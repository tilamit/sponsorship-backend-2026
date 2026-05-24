using Sponsorship.Domain.Entities;

namespace Sponsorship.Application.Common.Interfaces;

public interface ISponsorshipTypeRepository
{
    Task<IReadOnlyList<SponsorshipType>> ListAsync(bool activeOnly, CancellationToken ct = default);
    Task<SponsorshipType?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(SponsorshipType type, CancellationToken ct = default);
}
