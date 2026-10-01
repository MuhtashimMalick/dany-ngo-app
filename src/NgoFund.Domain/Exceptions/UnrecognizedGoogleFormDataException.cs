namespace NgoFund.Domain.Exceptions;

/// <summary>Raised by <c>GoogleFormSubmissionMapper</c> when the submission can't be mapped at
/// all — an unrecognized "Application Type" option, or an applicant CNIC that doesn't parse to 13
/// digits (the one CNIC on every form that's mandatory/unique, unlike a bride's/guarantor's/
/// mother's, which degrade to null + a note instead). Maps to 422 like every other
/// <see cref="DomainException"/>.</summary>
public sealed class UnrecognizedGoogleFormDataException(string reason) : DomainException(reason);
