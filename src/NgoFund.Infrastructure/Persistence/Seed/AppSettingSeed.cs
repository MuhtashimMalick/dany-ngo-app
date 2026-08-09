using Microsoft.EntityFrameworkCore;
using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Persistence.Seed;

/// <summary>Sane defaults for org-wide configuration — all editable later via the settings screen.</summary>
internal static class AppSettingSeed
{
    public static void Apply(ModelBuilder builder)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        (string Key, string Value, string DataType, string Description)[] rows =
        [
            ("OrgName", "NGO Fund Management", "string", "Organization name shown in the app header and on receipts."),
            ("Currency", "PKR", "string", "ISO currency code used for all monetary display."),
            ("ReceiptHeader", "", "string", "Text printed at the top of donation/payment receipts."),
            ("ReceiptFooter", "", "string", "Text printed at the bottom of donation/payment receipts."),
            ("FiscalYearStartMonth", "7", "number", "1-12; month the fiscal year starts (7 = July)."),
        ];

        builder.Entity<AppSetting>().HasData(rows.Select(r => new AppSetting
        {
            Id = DeterministicGuid.From($"app-setting:{r.Key}"),
            Key = r.Key,
            Value = r.Value,
            DataType = r.DataType,
            Description = r.Description,
            IsEditable = true,
            CreatedAt = now,
        }));
    }
}
