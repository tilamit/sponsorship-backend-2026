using Microsoft.EntityFrameworkCore;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Domain.Entities;
using Sponsorship.Domain.Enums;

namespace Sponsorship.Infrastructure.Persistence.Repositories;

public class SponsorshipRequestRepository : ISponsorshipRequestRepository
{
    private readonly AppDbContext _db;
    public SponsorshipRequestRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(SponsorshipRequest request, CancellationToken ct = default)
        => await _db.SponsorshipRequests.AddAsync(request, ct);

    public Task<SponsorshipRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.SponsorshipRequests
            .Include(r => r.Requestor)
            .Include(r => r.SponsorshipType)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<SponsorshipRequest?> GetByIdWithHistoryAsync(Guid id, CancellationToken ct = default)
        => _db.SponsorshipRequests
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Requestor)
            .Include(r => r.SponsorshipType)
            .Include(r => r.History).ThenInclude(h => h.ActionBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<SponsorshipRequest>> ListAllAsync(CancellationToken ct = default)
        => await _db.SponsorshipRequests
            .AsNoTracking()
            .Include(r => r.Requestor)
            .Include(r => r.SponsorshipType)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SponsorshipRequest>> ListByRequestorAsync(Guid requestorId, CancellationToken ct = default)
        => await _db.SponsorshipRequests
            .AsNoTracking()
            .Include(r => r.Requestor)
            .Include(r => r.SponsorshipType)
            .Where(r => r.RequestorId == requestorId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SponsorshipRequest>> ListByStatusAsync(RequestStatus status, CancellationToken ct = default)
        => await _db.SponsorshipRequests
            .AsNoTracking()
            .Include(r => r.Requestor)
            .Include(r => r.SponsorshipType)
            .Where(r => r.Status == status)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);
}
