using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace NgoFund.Infrastructure.Persistence;

/// <summary>A transaction handle whose <see cref="CommitAsync"/>/<see cref="IAsyncDisposable.DisposeAsync"/>
/// are no-ops when this call didn't actually open the transaction (see <see cref="DatabaseFacadeExtensions.BeginTransactionIfNoneAsync"/>).</summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// C3: <c>ApplicantService.CreateAsync</c> and
/// <c>ApplicationDetailsService.ReplaceGuarantorsAsync</c> each open their own transaction
/// so they work correctly called standalone — but the Google Form intake pipeline
/// (<c>GoogleFormIntakeService</c>) needs to run applicant + application + details + guarantors +
/// remarks as ONE outer transaction, and a plain <c>BeginTransactionAsync</c> throws
/// "A transaction is already started" the moment a nested call tries to open a second one on the
/// same connection. One helper, not a copy per service ( DRY rule).
/// </summary>
public static class DatabaseFacadeExtensions
{
    private sealed class OwnedTransactionScope(IDbContextTransaction transaction) : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class NoopTransactionScope : ITransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Begins a new transaction, unless one is already open on this <see cref="DatabaseFacade"/>
    /// (an outer caller's), in which case it returns a no-op handle and the outer transaction stays
    /// in sole control of commit/rollback.</summary>
    public static async Task<ITransactionScope> BeginTransactionIfNoneAsync(this DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        if (database.CurrentTransaction is not null)
        {
            return new NoopTransactionScope();
        }

        var transaction = await database.BeginTransactionAsync(cancellationToken);
        return new OwnedTransactionScope(transaction);
    }
}
