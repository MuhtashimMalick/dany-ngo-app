namespace NgoFund.Domain.Exceptions;

/// <summary>Raised when a client-supplied <c>documents.slot_key</c> doesn't fit the upload it's
/// attached to — an unrecognized slot for the target application's category, a document type not
/// accepted by that slot, or an owner (applicant/application vs. guarantor) that doesn't match the
/// slot's scope. A client-supplied string that drives the completeness/Approved gate must be
/// server-validated, never trusted as-is.</summary>
public sealed class DocumentSlotMismatchException(string reason) : DomainException(reason);
