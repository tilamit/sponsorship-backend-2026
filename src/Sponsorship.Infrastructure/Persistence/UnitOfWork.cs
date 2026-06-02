using System.Data;
using Microsoft.EntityFrameworkCore;
using Sponsorship.Application.Common.Interfaces;

namespace Sponsorship.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public UnitOfWork(AppDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        // EnableRetryOnFailure requires user-initiated transactions to run through
        // the execution strategy so the whole block can be retried atomically.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await operation(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
