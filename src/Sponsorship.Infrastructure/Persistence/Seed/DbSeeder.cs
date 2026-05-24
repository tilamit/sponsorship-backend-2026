using Microsoft.EntityFrameworkCore;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    private static readonly Guid RequestorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ManagerId   = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FinanceId   = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AdminId     = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SeedRolesAsync(db, ct);
        await SeedSponsorshipTypesAsync(db, ct);
        await SeedUsersAsync(db, hasher, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
    {
        var defaults = new[]
        {
            new Role(1, Role.Requestor),
            new Role(2, Role.Manager),
            new Role(3, Role.FinanceAdmin),
            new Role(4, Role.SystemAdmin)
        };
        foreach (var role in defaults)
        {
            if (!await db.Roles.AnyAsync(r => r.Id == role.Id, ct))
                await db.Roles.AddAsync(role, ct);
        }
    }

    private static async Task SeedSponsorshipTypesAsync(AppDbContext db, CancellationToken ct)
    {
        string[] defaults = { "Event", "Charity", "Sports", "Education", "CommunityOutreach" };
        foreach (var name in defaults)
        {
            if (!await db.SponsorshipTypes.AnyAsync(t => t.Name == name, ct))
                await db.SponsorshipTypes.AddAsync(new SponsorshipType(name), ct);
        }
    }

    private static async Task SeedUsersAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var pw = hasher.Hash("Demo@123");

        var users = new[]
        {
            new User(RequestorId, "requestor@demo.local", "Demo Requestor", "Sales", pw, 1, now),
            new User(ManagerId,   "manager@demo.local",   "Demo Manager",   "Sales", pw, 2, now),
            new User(FinanceId,   "finance@demo.local",   "Demo Finance",   "Finance", pw, 3, now),
            new User(AdminId,     "admin@demo.local",     "Demo Admin",     "IT", pw, 4, now)
        };

        foreach (var u in users)
        {
            if (!await db.Users.AnyAsync(x => x.Id == u.Id, ct))
                await db.Users.AddAsync(u, ct);
        }
    }
}
