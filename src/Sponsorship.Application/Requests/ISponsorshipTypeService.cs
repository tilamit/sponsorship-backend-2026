using Sponsorship.Application.Requests.Dtos;

namespace Sponsorship.Application.Requests;

public interface ISponsorshipTypeService
{
    Task<IReadOnlyList<SponsorshipTypeDto>> ListAsync(bool activeOnly, CancellationToken ct = default);
    Task<SponsorshipTypeDto> CreateAsync(CreateSponsorshipTypeDto dto, CancellationToken ct = default);
    Task<SponsorshipTypeDto> UpdateAsync(int id, UpdateSponsorshipTypeDto dto, CancellationToken ct = default);
}
