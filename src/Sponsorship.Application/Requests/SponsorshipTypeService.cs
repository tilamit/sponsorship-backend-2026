using Sponsorship.Application.Common.Exceptions;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Application.Requests.Mappers;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Application.Requests;

public class SponsorshipTypeService : ISponsorshipTypeService
{
    private const string CachePrefix = "sponsorship-types:";
    private static readonly TimeSpan TypesTtl = TimeSpan.FromMinutes(10);

    private readonly ISponsorshipTypeRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;

    public SponsorshipTypeService(ISponsorshipTypeRepository repo, IUnitOfWork uow, ICacheService cache)
    {
        _repo = repo;
        _uow = uow;
        _cache = cache;
    }

    public async Task<IReadOnlyList<SponsorshipTypeDto>> ListAsync(bool activeOnly, CancellationToken ct = default)
    {
        var key = activeOnly ? CachePrefix + "active" : CachePrefix + "all";
        var cached = await _cache.GetOrCreateAsync(
            key,
            async c => (IReadOnlyList<SponsorshipTypeDto>)(await _repo.ListAsync(activeOnly, c))
                .Select(t => t.ToDto())
                .ToList(),
            TypesTtl,
            ct);
        return cached ?? Array.Empty<SponsorshipTypeDto>();
    }

    public async Task<SponsorshipTypeDto> CreateAsync(CreateSponsorshipTypeDto dto, CancellationToken ct = default)
    {
        var entity = new SponsorshipType(dto.Name);
        await _repo.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        _cache.RemoveByPrefix(CachePrefix);
        return entity.ToDto();
    }

    public async Task<SponsorshipTypeDto> UpdateAsync(int id, UpdateSponsorshipTypeDto dto, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("SponsorshipType", id);
        entity.Rename(dto.Name);
        if (dto.IsActive) entity.Activate(); else entity.Deactivate();
        await _uow.SaveChangesAsync(ct);
        _cache.RemoveByPrefix(CachePrefix);
        return entity.ToDto();
    }
}
