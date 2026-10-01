namespace NgoFund.Domain.Exceptions;

public sealed class ApplicantRecordDeletedException(string fieldName, string value)
    : DomainException($"A deleted applicant record already holds {fieldName} '{value}'. Deleted applicants cannot be re-registered with the same {fieldName}; contact an administrator.");
