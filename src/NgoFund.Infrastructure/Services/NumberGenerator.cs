using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class NumberGenerator(AppDbContext dbContext) : INumberGenerator
{
    private static readonly Dictionary<string, string> Prefixes = new()
    {
        ["Donor"] = "DNR",
        ["Donation"] = "DON",
        ["Application"] = "APP",
        ["Payment"] = "PAY",
    };

    public async Task<string> NextAsync(string entityType, CancellationToken cancellationToken)
    {
        var prefix = Prefixes[entityType];
        var year = DateTime.UtcNow.Year;
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        // A single atomic UPSERT — INSERT the first row of the year or increment the existing
        // one, all in one round trip. This is what makes number generation collision-free under
        // concurrent requests without a separate SELECT ... FOR UPDATE step.
        //
        // NOTE: .ToListAsync() then .Single() client-side, not .SingleAsync() on the query —
        // EF Core's SqlQuery<T> composes extra SQL around operators like SingleAsync/Where/OrderBy
        // by wrapping the statement as a subquery, which is impossible for INSERT ... RETURNING
        // (only SELECT-shaped statements can be wrapped). ToListAsync executes the SQL as-is.
        var results = await dbContext.Database.SqlQuery<SequenceResult>(
            $"""
            INSERT INTO number_sequences (id, entity_type, year, prefix, current_value, padding, created_at)
            VALUES ({id}, {entityType}, {year}, {prefix}, 1, 5, {now})
            ON CONFLICT (entity_type, year) DO UPDATE
            SET current_value = number_sequences.current_value + 1
            RETURNING current_value, padding
            """).ToListAsync(cancellationToken);

        var result = results.Single();

        return $"{prefix}-{year}-{result.CurrentValue.ToString().PadLeft(result.Padding, '0')}";
    }

    private record SequenceResult(long CurrentValue, int Padding);
}
