using FluentAssertions;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Domain.Entities;
using Sponsorship.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sponsorship.Application.Tests.Infrastructure.Persistence;

public class UserRepositoryTests
{
    [Fact]
    public async Task GetByEmailAsync_returns_user_with_role_loaded()
    {
        var db = InMemoryDb.Create();
        db.Roles.Add(TestData.Role(2, Role.Manager));
        db.Users.Add(TestData.User(email: "manager@demo.local", roleId: 2));
        await db.SaveChangesAsync();
        var repo = new UserRepository(db);

        var user = await repo.GetByEmailAsync("manager@demo.local");

        user.Should().NotBeNull();
        user!.Role.Should().NotBeNull();
        user.Role.Name.Should().Be(Role.Manager);
    }

    [Fact]
    public async Task GetByEmailAsync_is_null_for_unknown_email()
    {
        var db = InMemoryDb.Create();
        var repo = new UserRepository(db);

        (await repo.GetByEmailAsync("nobody@demo.local")).Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_returns_user_with_role_loaded()
    {
        var db = InMemoryDb.Create();
        db.Roles.Add(TestData.Role(1, Role.Requestor));
        var user = TestData.User(roleId: 1);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var repo = new UserRepository(db);

        var found = await repo.GetByIdAsync(user.Id);

        found.Should().NotBeNull();
        found!.Role.Name.Should().Be(Role.Requestor);
    }
}
