namespace Sponsorship.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a single serializable database
    /// transaction. The read, the state mutation and the save it performs either
    /// all commit together or all roll back, so a workflow status change and its
    /// audit-history row can never be persisted half-way. Wrapped in the provider
    /// execution strategy so it cooperates with EnableRetryOnFailure.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
