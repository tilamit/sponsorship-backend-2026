using FluentAssertions;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Domain.Entities;
using Sponsorship.Domain.Enums;
using Sponsorship.Infrastructure.Persistence;
using Sponsorship.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sponsorship.Application.Tests.Infrastructure.Persistence;

/// Repository tests run against a real AppDbContext backed by EF Core's in-memory
/// provider — the "mock database". They verify query shape (filters, ordering,
/// eager-loading) without a SQL Server dependency.
public class SponsorshipRequestRepositoryTests
{
    private static (AppDbContext db, User user, SponsorshipType type) SeedContext()
    {
        var db = InMemoryDb.Create();
        var role = TestData.Role(1, Role.Requestor);
        var user = TestData.User(roleId: 1);
        var type = TestData.SponsorshipType(1, "Event");
        db.Roles.Add(role);
        db.Users.Add(user);
        db.SponsorshipTypes.Add(type);
        db.SaveChanges();
        return (db, user, type);
    }

    private static async Task<SponsorshipRequest> AddRequestAsync(
        AppDbContext db, Guid requestorId, int typeId,
        DateTime createdAtSeed, RequestStatus moveTo = RequestStatus.Draft)
    {
        var repo = new SponsorshipRequestRepository(db);
        var req = TestData.DraftRequest(requestorId: requestorId, sponsorshipTypeId: typeId);
        TestData.SetNavigation(req, nameof(SponsorshipRequest.CreatedAt), createdAtSeed);

        if (moveTo >= RequestStatus.PendingManagerApproval) req.Submit(requestorId, createdAtSeed);
        if (moveTo >= RequestStatus.PendingFinanceReview) req.ManagerApprove(requestorId, null, createdAtSeed);

        await repo.AddAsync(req);
        await db.SaveChangesAsync();
        return req;
    }

    [Fact]
    public async Task GetByIdAsync_eager_loads_requestor_and_type()
    {
        var (db, user, type) = SeedContext();
        var req = await AddRequestAsync(db, user.Id, type.Id, TestData.Now);
        var repo = new SponsorshipRequestRepository(db);

        var loaded = await repo.GetByIdAsync(req.Id);

        loaded.Should().NotBeNull();
        loaded!.Requestor.Should().NotBeNull();
        loaded.Requestor.FullName.Should().Be(user.FullName);
        loaded.SponsorshipType.Name.Should().Be("Event");
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_absent()
    {
        var (db, _, _) = SeedContext();
        var repo = new SponsorshipRequestRepository(db);

        (await repo.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task ListByStatusAsync_filters_to_requested_status()
    {
        var (db, user, type) = SeedContext();
        await AddRequestAsync(db, user.Id, type.Id, TestData.Now, RequestStatus.PendingManagerApproval);
        await AddRequestAsync(db, user.Id, type.Id, TestData.Now, RequestStatus.Draft);
        var repo = new SponsorshipRequestRepository(db);

        var pending = await repo.ListByStatusAsync(RequestStatus.PendingManagerApproval);

        pending.Should().ContainSingle();
        pending[0].Status.Should().Be(RequestStatus.PendingManagerApproval);
    }

    [Fact]
    public async Task ListByRequestorAsync_returns_only_that_requestors_rows()
    {
        var (db, user, type) = SeedContext();
        var other = TestData.User(email: "other@demo.local", roleId: 1);
        db.Users.Add(other);
        await db.SaveChangesAsync();

        await AddRequestAsync(db, user.Id, type.Id, TestData.Now);
        await AddRequestAsync(db, other.Id, type.Id, TestData.Now);
        var repo = new SponsorshipRequestRepository(db);

        var mine = await repo.ListByRequestorAsync(user.Id);

        mine.Should().ContainSingle();
        mine[0].RequestorId.Should().Be(user.Id);
    }

    [Fact]
    public async Task ListAllAsync_orders_by_created_descending()
    {
        var (db, user, type) = SeedContext();
        var older = await AddRequestAsync(db, user.Id, type.Id, TestData.Now.AddDays(-2));
        var newer = await AddRequestAsync(db, user.Id, type.Id, TestData.Now);
        var repo = new SponsorshipRequestRepository(db);

        var all = await repo.ListAllAsync();

        all.Select(r => r.Id).Should().ContainInOrder(newer.Id, older.Id);
    }

    [Fact]
    public async Task GetByIdWithHistoryAsync_includes_workflow_history()
    {
        var (db, user, type) = SeedContext();
        var req = await AddRequestAsync(db, user.Id, type.Id, TestData.Now, RequestStatus.PendingFinanceReview);
        var repo = new SponsorshipRequestRepository(db);

        var loaded = await repo.GetByIdWithHistoryAsync(req.Id);

        loaded.Should().NotBeNull();
        loaded!.History.Should().HaveCount(2); // Submit + ManagerApprove
        loaded.History.Select(h => h.Action)
            .Should().Contain(new[] { WorkflowAction.Submit, WorkflowAction.Approve });
    }
}
