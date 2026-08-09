namespace NgoFund.Domain.Exceptions;

public sealed class SettingNotEditableException(string key) : DomainException($"Setting '{key}' is not editable.");
