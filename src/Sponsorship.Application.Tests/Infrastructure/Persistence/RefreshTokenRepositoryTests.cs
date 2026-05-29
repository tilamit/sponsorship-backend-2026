using FluentAssertions;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sponsorship.Application.Tests.Infrastructure.Persistence;

public class RefreshTokenRepositoryTests
{
    [Fact]
    public async Task AddAsync_then_GetByTokenAsync_round_trips()
    {
        var db = InMemoryDb.Create();
        var repo = new RefreshTokenRepository(db);
        var token = TestData.RefreshToken(token: "abc123");

        await repo.AddAsync(token);
        await db.SaveChangesAsync();

        var found = await repo.GetByTokenAsync("abc123");
        found.Should().NotBeNull();
        found!.Id.Should().Be(token.Id);
    }

    [Fact]
    public async Task GetByTokenAsync_returns_null_for_unknown_token()
    {
        var db = InMemoryDb.Create();
        var repo = new RefreshTokenRepository(db);

        (await repo.GetByTokenAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task RevokeAllForUserAsync_revokes_only_that_users_active_tokens()
    {
        var db = InMemoryDb.Create();
        var repo = new RefreshTokenRepository(db);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var active1 = TestData.RefreshToken(userId: userId, token: "a");
        var active2 = TestData.RefreshToken(userId: userId, token: "b");
        var alreadyRevoked = TestData.RefreshToken(userId: userId, token: "c");
        alreadyRevoked.Revoke(TestData.Now.AddDays(-1));
        var othersToken = TestData.RefreshToken(userId: otherUserId, token: "d");

        await repo.AddAsync(active1);
        await repo.AddAsync(active2);
        await repo.AddAsync(alreadyRevoked);
        await repo.AddAsync(othersToken);
        await db.SaveChangesAsync();

        await repo.RevokeAllForUserAsync(userId, TestData.Now);
        await db.SaveChangesAsync();

        (await repo.GetByTokenAsync("a"))!.IsRevoked.Should().BeTrue();
        (await repo.GetByTokenAsync("b"))!.IsRevoked.Should().BeTrue();
        (await repo.GetByTokenAsync("c"))!.RevokedAt.Should().Be(TestData.Now.AddDays(-1),
            "an already-revoked token keeps its original timestamp");
        (await repo.GetByTokenAsync("d"))!.IsRevoked.Should().BeFalse(
            "another user's tokens must be left untouched");
    }
}
