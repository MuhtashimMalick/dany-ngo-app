-- ============================================================================================
-- ONE-TIME DATA REPAIR — application statuses corrupted by the manual-status-transition bug
-- ============================================================================================
--
-- Background: FundApplication.TransitionTo used to allow a reviewer to set an application's
-- status to Paid/PartiallyPaid by hand via the status-change endpoint (and the Desktop status
-- dropdown, which offers exactly what that endpoint allows), with no check against actual
-- payments recorded against it. That path is now closed — Paid/PartiallyPaid are reachable ONLY
-- via FundApplication.RecomputeStatusFromPayments, which derives them from
-- SUM(completed payments) — but any application that was already wrongly marked before the fix
-- shipped stays wrong until it happens to receive another payment or void. This script finds and
-- corrects those rows directly.
--
-- What it does: for every application currently Approved/PartiallyPaid/Paid, recomputes what its
-- status SHOULD be from SUM(completed payments) against it, using the exact same logic as
-- FundApplication.RecomputeStatusFromPayments:
--     total_completed_paid >= approved_amount  -> Paid
--     total_completed_paid > 0                 -> PartiallyPaid
--     otherwise                                -> Approved
-- For every row that would actually change, it (a) inserts an application_status_history row
-- documenting the correction, then (b) updates applications.status to the corrected value.
--
-- changed_by is left NULL on the history rows this script inserts: application_status_history.
-- changed_by is nullable precisely for system/script actions with no specific acting user (see
-- docs/schema.md) — there is no reviewer to attribute this correction to, so NULL is used rather
-- than guessing a seed/admin user id.
--
-- Idempotent: safe to run more than once. After the first run, every application's status already
-- matches what this script would compute, so a second run updates zero rows and inserts no
-- history.
--
-- Not wired into the EF migrator — this is a one-shot the operator runs manually once, after
-- deploying the fix, e.g.:
--     docker compose exec -T db psql -U ngofund -d ngofund -f - < docs/ops/repair-application-statuses.sql
-- or, from a shell inside the db container:
--     psql -U ngofund -d ngofund -f /path/to/repair-application-statuses.sql
--
-- Delete or archive this file once it has been run against the target database.
-- ============================================================================================

BEGIN;

-- Step 1: journal the correction for every application whose status is about to change.
WITH recomputed AS (
    SELECT
        a.id,
        a.status AS old_status,
        CASE
            WHEN a.approved_amount IS NOT NULL
                 AND COALESCE(SUM(p.amount) FILTER (WHERE p.status = 'Completed'), 0) >= a.approved_amount
                THEN 'Paid'
            WHEN COALESCE(SUM(p.amount) FILTER (WHERE p.status = 'Completed'), 0) > 0
                THEN 'PartiallyPaid'
            ELSE 'Approved'
        END AS correct_status
    FROM applications a
    LEFT JOIN payments p ON p.application_id = a.id
    WHERE a.status IN ('Approved', 'PartiallyPaid', 'Paid')
    GROUP BY a.id, a.status, a.approved_amount
)
INSERT INTO application_status_history (id, application_id, from_status, to_status, remarks, changed_at, changed_by, created_at, created_by)
SELECT
    uuidv7(), -- PostgreSQL 18 native UUIDv7 generator, matching Guid.CreateVersion7() elsewhere in the app.
    r.id,
    r.old_status,
    r.correct_status,
    'Data repair: status recomputed from actual completed payments (was incorrectly set via a manual transition that predates the fix for this bug).',
    now(),
    NULL,
    now(),
    NULL
FROM recomputed r
WHERE r.old_status <> r.correct_status;

-- Step 2: apply the correction. Same recompute logic, re-evaluated from the same underlying
-- (unmodified-by-step-1) tables, so this only ever touches the exact rows just journalled above.
WITH recomputed AS (
    SELECT
        a.id,
        CASE
            WHEN a.approved_amount IS NOT NULL
                 AND COALESCE(SUM(p.amount) FILTER (WHERE p.status = 'Completed'), 0) >= a.approved_amount
                THEN 'Paid'
            WHEN COALESCE(SUM(p.amount) FILTER (WHERE p.status = 'Completed'), 0) > 0
                THEN 'PartiallyPaid'
            ELSE 'Approved'
        END AS correct_status
    FROM applications a
    LEFT JOIN payments p ON p.application_id = a.id
    WHERE a.status IN ('Approved', 'PartiallyPaid', 'Paid')
    GROUP BY a.id, a.status, a.approved_amount
)
UPDATE applications a
SET status = r.correct_status,
    updated_at = now()
FROM recomputed r
WHERE a.id = r.id
  AND a.status <> r.correct_status;

COMMIT;
