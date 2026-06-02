using Sponsorship.Application.Common.Exceptions;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Application.Requests.Mappers;
using Sponsorship.Domain.Entities;
using Sponsorship.Domain.Enums;

namespace Sponsorship.Application.Requests;

public class SponsorshipRequestService : ISponsorshipRequestService
{
    private readonly ISponsorshipRequestRepository _requests;
    private readonly ISponsorshipTypeRepository _types;
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public SponsorshipRequestService(
        ISponsorshipRequestRepository requests,
        ISponsorshipTypeRepository types,
        IUserRepository users,
        ICurrentUserService current,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _requests = requests;
        _types = types;
        _users = users;
        _current = current;
        _clock = clock;
        _uow = uow;
    }

    public async Task<SponsorshipRequestDto> CreateAsync(CreateRequestDto dto, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        var type = await _types.GetByIdAsync(dto.SponsorshipTypeId, ct)
            ?? throw new NotFoundException("SponsorshipType", dto.SponsorshipTypeId);
        if (!type.IsActive)
            throw new Sponsorship.Domain.Exceptions.DomainException("Sponsorship type is inactive.");

        var entity = new SponsorshipRequest(
            Guid.NewGuid(), dto.Title, userId, dto.Department, dto.SponsorshipTypeId,
            dto.EventName, dto.EventDate, dto.RequestedAmount, dto.Purpose,
            dto.ExpectedBenefit, dto.Remarks, _clock.UtcNow);

        await _requests.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        var loaded = await _requests.GetByIdAsync(entity.Id, ct)
            ?? throw new NotFoundException("SponsorshipRequest", entity.Id);
        return loaded.ToDto();
    }

    public async Task<SponsorshipRequestDto> UpdateAsync(Guid id, UpdateRequestDto dto, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var entity = await _requests.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("SponsorshipRequest", id);
        if (entity.RequestorId != userId)
            throw new ForbiddenException("You can only edit your own requests.");

        var type = await _types.GetByIdAsync(dto.SponsorshipTypeId, ct)
            ?? throw new NotFoundException("SponsorshipType", dto.SponsorshipTypeId);
        if (!type.IsActive)
            throw new Sponsorship.Domain.Exceptions.DomainException("Sponsorship type is inactive.");

        entity.UpdateDraft(dto.Title, dto.Department, dto.SponsorshipTypeId,
            dto.EventName, dto.EventDate, dto.RequestedAmount, dto.Purpose,
            dto.ExpectedBenefit, dto.Remarks, _clock.UtcNow);

        await _uow.SaveChangesAsync(ct);

        var reloaded = await _requests.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("SponsorshipRequest", id);
        return reloaded.ToDto();
    }

    public async Task<SponsorshipRequestDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _requests.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("SponsorshipRequest", id);
        EnsureCanRead(entity);
        return entity.ToDto();
    }

    public async Task<IReadOnlyList<SponsorshipRequestDto>> ListForCurrentUserAsync(CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var role = _current.Role ?? string.Empty;
        IReadOnlyList<SponsorshipRequest> items;

        if (role is Role.SystemAdmin or Role.Manager or Role.FinanceAdmin)
            items = await _requests.ListAllAsync(ct);
        else
            items = await _requests.ListByRequestorAsync(userId, ct);

        return items.Select(r => r.ToDto()).ToList();
    }

    public async Task SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        // Draft → PendingManagerApproval: status change and history row commit atomically.
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            var entity = await _requests.GetByIdAsync(id, token)
                ?? throw new NotFoundException("SponsorshipRequest", id);
            if (entity.RequestorId != userId)
                throw new ForbiddenException("You can only submit your own requests.");

            entity.Submit(userId, _clock.UtcNow);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    public async Task CancelAsync(Guid id, string? remarks, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            var entity = await _requests.GetByIdAsync(id, token)
                ?? throw new NotFoundException("SponsorshipRequest", id);
            if (entity.RequestorId != userId)
                throw new ForbiddenException("You can only cancel your own requests.");

            entity.Cancel(userId, remarks, _clock.UtcNow);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    private Guid RequireUserId()
        => _current.UserId ?? throw new UnauthorizedException("No authenticated user.");

    private void EnsureCanRead(SponsorshipRequest entity)
    {
        var role = _current.Role ?? string.Empty;
        if (role is Role.SystemAdmin or Role.Manager or Role.FinanceAdmin) return;
        var userId = RequireUserId();
        if (entity.RequestorId != userId)
            throw new ForbiddenException("You cannot view this request.");
    }
}
