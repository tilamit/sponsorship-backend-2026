using Sponsorship.Application.Requests.Dtos;

namespace Sponsorship.Application.Requests;

public interface ISponsorshipRequestService
{
    Task<SponsorshipRequestDto> CreateAsync(CreateRequestDto dto, CancellationToken ct = default);
    Task<SponsorshipRequestDto> UpdateAsync(Guid id, UpdateRequestDto dto, CancellationToken ct = default);
    Task<SponsorshipRequestDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SponsorshipRequestDto>> ListForCurrentUserAsync(CancellationToken ct = default);
    Task SubmitAsync(Guid id, CancellationToken ct = default);
    Task CancelAsync(Guid id, string? remarks, CancellationToken ct = default);
}
