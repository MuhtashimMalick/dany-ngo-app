namespace NgoFund.Contracts.Settings;

public record AppSettingDto(string Key, string? Value, string DataType, string? Description, bool IsEditable);
