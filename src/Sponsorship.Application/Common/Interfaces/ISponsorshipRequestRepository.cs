using Sponsorship.Domain.Entities;
using Sponsorship.Domain.Enums;

namespace Sponsorship.Application.Common.Interfaces;

public interface ISponsorshipRequestRepository
{
    Task AddAsync(SponsorshipRequest request, CancellationToken ct = default);
    Task<SponsorshipRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SponsorshipRequest?> GetByIdWithHistoryAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SponsorshipRequest>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SponsorshipRequest>> ListByRequestorAsync(Guid requestorId, CancellationToken ct = default);
    Task<IReadOnlyList<SponsorshipRequest>> ListByStatusAsync(RequestStatus status, CancellationToken ct = default);
}
