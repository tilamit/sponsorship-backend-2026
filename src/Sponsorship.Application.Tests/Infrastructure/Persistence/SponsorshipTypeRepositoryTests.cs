using FluentAssertions;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Domain.Entities;
using Sponsorship.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sponsorship.Application.Tests.Infrastructure.Persistence;

public class SponsorshipTypeRepositoryTests
{
    [Fact]
    public async Task ListAsync_active_only_excludes_disabled_types()
    {
        var db = InMemoryDb.Create();
        db.SponsorshipTypes.Add(TestData.SponsorshipType(1, "Event"));
        db.SponsorshipTypes.Add(TestData.SponsorshipType(2, "Retired", active: false));
        await db.SaveChangesAsync();
        var repo = new SponsorshipTypeRepository(db);

        var active = await repo.ListAsync(activeOnly: true);

        active.Should().ContainSingle(t => t.Name == "Event");
    }

    [Fact]
    public async Task ListAsync_all_returns_everything_ordered_by_name()
    {
        var db = InMemoryDb.Create();
        db.SponsorshipTypes.Add(TestData.SponsorshipType(1, "Sports"));
        db.SponsorshipTypes.Add(TestData.SponsorshipType(2, "Charity"));
        await db.SaveChangesAsync();
        var repo = new SponsorshipTypeRepository(db);

        var all = await repo.ListAsync(activeOnly: false);

        all.Select(t => t.Name).Should().ContainInOrder("Charity", "Sports");
    }

    [Fact]
    public async Task GetByIdAsync_returns_match_or_null()
    {
        var db = InMemoryDb.Create();
        db.SponsorshipTypes.Add(TestData.SponsorshipType(7, "Education"));
        await db.SaveChangesAsync();
        var repo = new SponsorshipTypeRepository(db);

        (await repo.GetByIdAsync(7))!.Name.Should().Be("Education");
        (await repo.GetByIdAsync(404)).Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_persists_a_new_type()
    {
        var db = InMemoryDb.Create();
        var repo = new SponsorshipTypeRepository(db);

        await repo.AddAsync(new SponsorshipType("CommunityOutreach"));
        await db.SaveChangesAsync();

        var all = await repo.ListAsync(activeOnly: false);
        all.Should().ContainSingle(t => t.Name == "CommunityOutreach" && t.IsActive);
    }
}
