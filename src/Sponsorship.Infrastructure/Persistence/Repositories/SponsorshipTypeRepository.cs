using Microsoft.EntityFrameworkCore;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Infrastructure.Persistence.Repositories;

public class SponsorshipTypeRepository : ISponsorshipTypeRepository
{
    private readonly AppDbContext _db;
    public SponsorshipTypeRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SponsorshipType>> ListAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = _db.SponsorshipTypes.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(t => t.IsActive);
        return await q.OrderBy(t => t.Name).ToListAsync(ct);
    }

    public Task<SponsorshipType?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.SponsorshipTypes.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(SponsorshipType type, CancellationToken ct = default)
        => await _db.SponsorshipTypes.AddAsync(type, ct);
}
