using Microsoft.EntityFrameworkCore;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;
    public RefreshTokenRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        => await _db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        => _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

    public async Task RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active) t.Revoke(now);
    }
}
