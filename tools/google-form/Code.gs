/**
 * Google Form -> NGO Fund Management System intake bridge.
 *
 * One file, no dependencies (per design brief D). Bound to the ONE intake form
 * ("A.Z Dany Welfare Trust — Application Form"), whose "Application Type" radio branches into six
 * sections (Housing/Marriage/Business Loan/Education/Health/General Assistance) — install once.
 * See README.md for one-time setup.
 *
 * Flow: onFormSubmit appends a Pending row to this spreadsheet's "Intake Status" tab (never the
 * Form Responses tab — Google rewrites that tab whenever the form's questions change, which would
 * silently blow away any columns we added there) and then attempts delivery. A time-based trigger
 * (retryPending, every 15 minutes) re-attempts anything still Pending/Failed/Submitted (Submitted
 * = the application exists but not every file has landed yet), with NO cap on total attempts — the
 * API server runs on staff's own PC and may be off for the night — but a cap on how many rows one
 * run processes, to stay inside Apps Script's execution quotas. Each retryPending run also
 * reconciles the sheet against form.getResponses() first, so a submission whose onFormSubmit run
 * couldn't get the lock (and so never got its own Pending row) is never lost.
 */

// ---------- configuration ----------

var BASE_URL = 'https://unrupturable-unreticently-carl.ngrok-free.dev';
var SHEET_NAME = 'Intake Status';
var RETRY_MINUTES = 15;
var MAX_ROWS_PER_RETRY_RUN = 20;
var SUPPORTED_MIME_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'];
var REJECTABLE_HTTP_CODES = [400, 404, 413, 415, 422];
var HEADERS = [
  'ResponseId', 'SubmittedAt', 'ApplicationType', 'ApplicantName', 'Status',
  'ApplicationNumber', 'Attempts', 'LastAttemptAt', 'LastError', 'FileNotes',
];

// ---------- one-time setup ----------

/** Run this once (manually, from the Apps Script editor), on the one form. Re-running is safe —
 * it replaces its own triggers rather than duplicating them. */
function setup() {
  var form = FormApp.getActiveForm();

  ScriptApp.getProjectTriggers().forEach(function (t) {
    var fn = t.getHandlerFunction();
    if (fn === 'onFormSubmit' || fn === 'retryPending') {
      ScriptApp.deleteTrigger(t);
    }
  });

  ScriptApp.newTrigger('onFormSubmit').forForm(form).onFormSubmit().create();
  ScriptApp.newTrigger('retryPending').timeBased().everyMinutes(RETRY_MINUTES).create();

  ensureSheet_();

  var apiKey = getApiKey_();
  var ping = getJson_(BASE_URL + '/api/intake/google-form/ping', apiKey);
  Logger.log(ping.ok ? 'Setup complete. Intake ping OK.' : 'Setup complete, but the intake ping FAILED: ' + (ping.detail || ping.code) + ' — check INTAKE_API_KEY and that the API/ngrok tunnel are running.');
}

/** Reads the API key from Script Properties — NEVER hardcoded here. Project Settings > Script
 * Properties > add INTAKE_API_KEY = <the same value as GOOGLE_FORM_INTAKE_API_KEY in the API's .env>. */
function getApiKey_() {
  var key = PropertiesService.getScriptProperties().getProperty('INTAKE_API_KEY');
  if (!key) {
    throw new Error('Script property INTAKE_API_KEY is not set. Project Settings > Script Properties.');
  }
  return key;
}

/** The "Intake Status" tab lives in the form's destination spreadsheet, as its OWN tab — never
 * columns bolted onto "Form Responses 1", which Google silently rewrites whenever the form's
 * questions change. */
function ensureSheet_() {
  var form = FormApp.getActiveForm();
  var destinationId = form.getDestinationId();
  if (!destinationId) {
    throw new Error('This form has no destination spreadsheet yet — link one (Responses tab > green Sheets icon) before running setup().');
  }

  var spreadsheet = SpreadsheetApp.openById(destinationId);
  var sheet = spreadsheet.getSheetByName(SHEET_NAME);
  if (!sheet) {
    sheet = spreadsheet.insertSheet(SHEET_NAME);
  }

  var firstRow = sheet.getRange(1, 1, 1, HEADERS.length).getValues()[0];
  var hasHeaders = HEADERS.every(function (h, i) { return firstRow[i] === h; });
  if (!hasHeaders) {
    sheet.getRange(1, 1, 1, HEADERS.length).setValues([HEADERS]);
  }

  return sheet;
}

// ---------- form submit ----------

function onFormSubmit(e) {
  // J1: the Pending row is appended FIRST, with no lock held — a submission must never be lost
  // just because retryPending happened to be mid-flight on the same script (20 rows plus
  // multi-MB uploads can easily hold the lock past 30s). Only the delivery ATTEMPT below needs
  // the lock, and if it can't get one within 30s, the row is already on the sheet — the next
  // retryPending sweep picks it up as an ordinary Pending row.
  var sheet = ensureSheet_();
  var formResponse = e.response;
  var rowIndex = appendPendingRow_(sheet, formResponse);

  var lock = LockService.getScriptLock();
  if (!lock.tryLock(30000)) {
    return;
  }

  try {
    processResponseById_(formResponse.getId(), sheet, rowIndex);
  } finally {
    lock.releaseLock();
  }
}

/** Time-driven, every 15 minutes: retries anything not yet Complete/Rejected, oldest first, capped
 * per run to respect Apps Script's execution-time quota. Reconciles first (J1 safety net): any
 * form.getResponses() id missing from the sheet — e.g. an onFormSubmit run that couldn't get the
 * lock at all — gets a Pending row appended before the sweep below runs. */
function retryPending() {
  var lock = LockService.getScriptLock();
  if (!lock.tryLock(30000)) {
    return;
  }

  try {
    var sheet = ensureSheet_();
    reconcileMissingResponses_(sheet);

    var data = sheet.getDataRange().getValues();
    var idCol = HEADERS.indexOf('ResponseId');
    var statusCol = HEADERS.indexOf('Status');
    var processed = 0;

    for (var r = 1; r < data.length && processed < MAX_ROWS_PER_RETRY_RUN; r++) {
      var status = data[r][statusCol];
      if (status === 'Pending' || status === 'Failed' || status === 'Submitted') {
        processResponseById_(data[r][idCol], sheet, r + 1);
        processed++;
      }
    }
  } finally {
    lock.releaseLock();
  }
}

// ponytail: form.getResponses() re-fetches EVERY response the form has ever received, every 15
// minutes — fine at this form's scale, but the cost grows without bound as responses pile up.
// Upgrade path if that ever bites the execution-time quota: track a "last known response count"
// in Script Properties and skip this scan when form.getResponses().length hasn't changed.
function reconcileMissingResponses_(sheet) {
  var data = sheet.getDataRange().getValues();
  var idCol = HEADERS.indexOf('ResponseId');
  var knownIds = {};
  for (var r = 1; r < data.length; r++) {
    knownIds[data[r][idCol]] = true;
  }

  var form = FormApp.getActiveForm();
  form.getResponses().forEach(function (formResponse) {
    if (!knownIds[formResponse.getId()]) {
      appendPendingRow_(sheet, formResponse);
    }
  });
}

// ---------- core processing ----------

function processResponseById_(responseId, sheet, rowIndex) {
  var apiKey = getApiKey_();
  var form = FormApp.getActiveForm();

  var formResponse;
  try {
    formResponse = form.getResponse(responseId);
  } catch (err) {
    setRow_(sheet, rowIndex, { Status: 'Rejected', LastError: 'Form response not found: ' + err.message, LastAttemptAt: new Date() });
    return;
  }

  var built = buildPayload_(formResponse);
  bumpAttempts_(sheet, rowIndex);

  var submitResult = postJson_(BASE_URL + '/api/intake/google-form/submissions', apiKey, built.payload);

  if (!submitResult.ok) {
    setRow_(sheet, rowIndex, {
      Status: submitResult.terminal ? 'Rejected' : 'Failed',
      LastError: submitResult.detail || ('HTTP ' + submitResult.code),
      LastAttemptAt: new Date(),
    });
    return;
  }

  var body = JSON.parse(submitResult.text);
  setRow_(sheet, rowIndex, { ApplicationNumber: body.applicationNumber, LastAttemptAt: new Date(), LastError: '' });

  var fileResults = uploadFiles_(apiKey, built.payload.formResponseId, built.fileEntries);
  var allDone = fileResults.every(function (r) { return r.ok; });

  setRow_(sheet, rowIndex, {
    Status: allDone ? 'Complete' : 'Submitted',
    FileNotes: fileResults.map(function (r) { return r.note; }).filter(Boolean).join('; '),
  });
}

/** Builds the JSON-safe submission payload PLUS the internal file entries (with live Blobs, never
 * serialized into the payload itself) that {@link uploadFiles_} sends as separate multipart calls. */
function buildPayload_(formResponse) {
  var answers = [];
  var fileEntries = [];
  var applicationType = '';

  formResponse.getItemResponses().forEach(function (itemResponse) {
    var item = itemResponse.getItem();
    var title = item.getTitle();
    var helpText = item.getHelpText() || null;

    if (item.getType() === FormApp.ItemType.FILE_UPLOAD) {
      var fileIds = itemResponse.getResponse() || [];
      fileEntries = fileEntries.concat(resolveFileEntries_(title, helpText, fileIds));
      return;
    }

    var raw = itemResponse.getResponse();
    var values = Array.isArray(raw) ? raw.map(String) : [String(raw)];
    answers.push({ title: title, helpText: helpText, values: values });

    if (isApplicationTypeTitle_(title)) {
      applicationType = values[0] || '';
    }
  });

  return {
    payload: {
      formResponseId: formResponse.getId(),
      submittedAt: formResponse.getTimestamp().toISOString(),
      respondentEmail: formResponse.getRespondentEmail() || null,
      applicationType: applicationType,
      answers: answers,
      files: fileEntries.map(function (f) {
        return { questionTitle: f.questionTitle, questionHelpText: f.questionHelpText, driveFileId: f.driveFileId, fileName: f.fileName, mimeType: f.mimeType };
      }),
    },
    fileEntries: fileEntries,
  };
}

/** One entry per uploaded file (a single FILE_UPLOAD question can carry several, e.g. the Business
 * Loan form's combined upload). Google-native docs (Sheets/Docs/Slides) are converted to PDF; any
 * other non jpeg/png/webp/pdf file is tried as JPEG, and skipped (never fails the whole
 * submission) if that also doesn't work — the server still gets a manifest entry for it and adds
 * its own "unsupported file" note either way. */
function resolveFileEntries_(title, helpText, fileIds) {
  return fileIds.map(function (id) {
    var file = DriveApp.getFileById(id);
    var rawMime = file.getMimeType();
    var blob = null;
    var mime = rawMime;
    var skip = false;

    if (rawMime.indexOf('application/vnd.google-apps.') === 0) {
      try {
        blob = file.getAs('application/pdf');
        mime = 'application/pdf';
      } catch (err) {
        skip = true;
      }
    } else if (SUPPORTED_MIME_TYPES.indexOf(rawMime) !== -1) {
      blob = file.getBlob();
    } else {
      try {
        blob = file.getAs('image/jpeg');
        mime = 'image/jpeg';
      } catch (err) {
        skip = true;
      }
    }

    return {
      questionTitle: title, questionHelpText: helpText, driveFileId: id,
      fileName: file.getName(), mimeType: mime, blob: blob, skip: skip,
    };
  });
}

function uploadFiles_(apiKey, formResponseId, fileEntries) {
  return fileEntries.map(function (entry) {
    if (entry.skip || !entry.blob) {
      return { ok: false, note: 'Skipped unsupported file for "' + entry.questionTitle + '" (' + entry.fileName + ')' };
    }

    var url = BASE_URL + '/api/intake/google-form/submissions/' + encodeURIComponent(formResponseId) + '/documents';
    var response;
    try {
      response = UrlFetchApp.fetch(url, {
        method: 'post',
        headers: { 'X-Intake-Key': apiKey, 'ngrok-skip-browser-warning': 'true' },
        payload: {
          questionTitle: entry.questionTitle,
          questionHelpText: entry.questionHelpText || '',
          driveFileId: entry.driveFileId,
          file: entry.blob,
        },
        muteHttpExceptions: true,
      });
    } catch (err) {
      return { ok: false, note: 'Upload network error for "' + entry.fileName + '": ' + err.message };
    }

    var code = response.getResponseCode();
    if (code === 200 || code === 201) {
      return { ok: true, note: '' };
    }

    // A GENUINE rejectable 4xx (415 unsupported type, etc. — real ProblemDetails JSON from the API
    // itself) is "handled", not retried forever, and recorded so staff can see it. Anything else —
    // HTML, or ngrok's own offline page — is a Failed/retryable network-shaped failure (J2), same
    // rule as postJson_.
    var terminal = isTerminalRejection_(code, response.getContentText(), response);
    var note = 'Upload ' + (terminal ? 'rejected' : 'failed') +
      ' (' + code + ') for "' + entry.fileName + '": ' + extractProblemDetail_(response.getContentText());
    return { ok: terminal, note: note };
  });
}

// ---------- HTTP helpers ----------

function postJson_(url, apiKey, payload) {
  var response;
  try {
    response = UrlFetchApp.fetch(url, {
      method: 'post',
      contentType: 'application/json',
      payload: JSON.stringify(payload),
      headers: { 'X-Intake-Key': apiKey, 'ngrok-skip-browser-warning': 'true' },
      muteHttpExceptions: true,
    });
  } catch (err) {
    return { ok: false, code: 0, terminal: false, detail: 'Network error: ' + err.message };
  }

  var code = response.getResponseCode();
  var text = response.getContentText();
  if (code === 200 || code === 201) {
    return { ok: true, code: code, text: text };
  }
  return { ok: false, code: code, text: text, terminal: isTerminalRejection_(code, text, response), detail: extractProblemDetail_(text) };
}

/** J2: a 4xx is terminal (Rejected, never retried again) only when the body genuinely parses as
 * the API's own RFC-9457 ProblemDetails JSON (a numeric `status` field). Everything else — an HTML
 * page, or a response carrying an ngrok error header — is a Failed/retryable network-shaped
 * failure. This matters because ngrok answers a 404 ERR_NGROK_3200 "endpoint offline" HTML page
 * when staff's PC or the tunnel agent is off overnight (404 is in REJECTABLE_HTTP_CODES for the
 * API's OWN genuine 404s), which must NOT be treated the same as the API rejecting the data —
 * that's exactly the scenario retryPending exists for. */
function isTerminalRejection_(code, responseText, response) {
  if (REJECTABLE_HTTP_CODES.indexOf(code) === -1) {
    return false;
  }
  if (hasNgrokErrorHeader_(response)) {
    return false;
  }
  try {
    var problem = JSON.parse(responseText);
    return typeof problem.status === 'number';
  } catch (err) {
    return false;
  }
}

function hasNgrokErrorHeader_(response) {
  var headers = response.getAllHeaders();
  return Object.keys(headers).some(function (name) { return name.toLowerCase() === 'ngrok-error-code'; });
}

function getJson_(url, apiKey) {
  var response;
  try {
    response = UrlFetchApp.fetch(url, {
      method: 'get',
      headers: { 'X-Intake-Key': apiKey, 'ngrok-skip-browser-warning': 'true' },
      muteHttpExceptions: true,
    });
  } catch (err) {
    return { ok: false, code: 0, detail: 'Network error: ' + err.message };
  }

  var code = response.getResponseCode();
  return code === 200
    ? { ok: true, code: code, text: response.getContentText() }
    : { ok: false, code: code, detail: extractProblemDetail_(response.getContentText()) };
}

/** RFC-9457 ProblemDetails — pulls .detail (falling back to .title), or the raw body if it isn't
 * JSON at all (an ngrok offline/interstitial HTML page, say). */
function extractProblemDetail_(text) {
  try {
    var problem = JSON.parse(text);
    return problem.detail || problem.title || text.substring(0, 300);
  } catch (err) {
    return (text || '').substring(0, 300);
  }
}

// ---------- sheet helpers ----------

function appendPendingRow_(sheet, formResponse) {
  var applicationType = '';
  var applicantName = '';

  formResponse.getItemResponses().forEach(function (itemResponse) {
    var item = itemResponse.getItem();
    if (item.getType() === FormApp.ItemType.FILE_UPLOAD) {
      return;
    }
    var title = normalizeTitle_(item.getTitle());
    var raw = itemResponse.getResponse();
    var value = Array.isArray(raw) ? raw.join(', ') : String(raw);

    if (isApplicationTypeTitle_(title)) {
      applicationType = value;
    } else if (!applicantName && /name/i.test(title) && !/(father|mother|grandfather|guarantor|bride|groom|previous|business|responsible)/i.test(title)) {
      applicantName = value;
    }
  });

  var row = [
    formResponse.getId(), formResponse.getTimestamp(), applicationType, applicantName,
    'Pending', '', 0, '', '', '',
  ];
  sheet.appendRow(row);
  return sheet.getLastRow();
}

function bumpAttempts_(sheet, rowIndex) {
  var col = HEADERS.indexOf('Attempts') + 1;
  var cell = sheet.getRange(rowIndex, col);
  cell.setValue((Number(cell.getValue()) || 0) + 1);
}

/** Writes only the named columns for one row — every other column is left untouched. */
function setRow_(sheet, rowIndex, fields) {
  Object.keys(fields).forEach(function (name) {
    var col = HEADERS.indexOf(name);
    if (col === -1) {
      throw new Error('Unknown Intake Status column: ' + name);
    }
    sheet.getRange(rowIndex, col + 1).setValue(fields[name]);
  });
}

function normalizeTitle_(title) {
  return String(title).trim().replace(/[–—]/g, '-').replace(/\s+/g, ' ');
}

/** The live form's question titles carry a trailing " (Urdu translation)" suffix (e.g.
 * "Application Type (درخواست کی قسم)") — normalizeTitle_ alone doesn't strip that, so an exact
 * '=== Application Type' compare never matched and applicationType silently stayed '' for every
 * submission. Mirrors FormValueParser.StripOptionParenthetical on the C# side (same " (" cut,
 * applied here to a title instead of an option value). */
function stripParenthetical_(text) {
  var trimmed = String(text).trim();
  var parenIndex = trimmed.indexOf(' (');
  return parenIndex > 0 ? trimmed.substring(0, parenIndex).trim() : trimmed;
}

function isApplicationTypeTitle_(title) {
  var normalized = normalizeTitle_(title);
  return normalized === 'Application Type' || stripParenthetical_(normalized) === 'Application Type';
}
