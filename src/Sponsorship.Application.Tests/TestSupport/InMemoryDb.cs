using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sponsorship.Infrastructure.Persistence;

namespace Sponsorship.Application.Tests.TestSupport;

/// Spins up an isolated EF Core in-memory database per call so repository tests
/// ("mock database") never share state. This exercises the real AppDbContext,
/// entity configurations, and LINQ queries without a SQL Server dependency.
public static class InMemoryDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            // The relational default-value-sql / split-query hints are no-ops on the
            // in-memory provider; silence the transaction warning for cleanliness.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
}
