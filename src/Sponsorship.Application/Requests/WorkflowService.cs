using Sponsorship.Application.Common.Exceptions;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Application.Requests.Mappers;
using Sponsorship.Domain.Entities;
using Sponsorship.Domain.Enums;

namespace Sponsorship.Application.Requests;

public class WorkflowService : IWorkflowService
{
    private const string HistoryCachePrefix = "workflow-history:";
    private static readonly TimeSpan HistoryTtl = TimeSpan.FromSeconds(30);

    private readonly ISponsorshipRequestRepository _requests;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;

    public WorkflowService(
        ISponsorshipRequestRepository requests,
        ICurrentUserService current,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        ICacheService cache)
    {
        _requests = requests;
        _current = current;
        _clock = clock;
        _uow = uow;
        _cache = cache;
    }

    public async Task<IReadOnlyList<SponsorshipRequestDto>> ListPendingManagerAsync(CancellationToken ct = default)
    {
        var items = await _requests.ListByStatusAsync(RequestStatus.PendingManagerApproval, ct);
        return items.Select(r => r.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<SponsorshipRequestDto>> ListPendingFinanceAsync(CancellationToken ct = default)
    {
        var items = await _requests.ListByStatusAsync(RequestStatus.PendingFinanceReview, ct);
        return items.Select(r => r.ToDto()).ToList();
    }

    public async Task ManagerDecisionAsync(Guid requestId, ApprovalActionDto action, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var entity = await _requests.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException("SponsorshipRequest", requestId);

        if (action.Action == ApprovalDecision.Approve)
            entity.ManagerApprove(userId, action.Remarks, _clock.UtcNow);
        else
            entity.ManagerReject(userId, action.Remarks, _clock.UtcNow);

        await _uow.SaveChangesAsync(ct);
        _cache.Remove(HistoryKey(requestId));
    }

    public async Task FinanceDecisionAsync(Guid requestId, ApprovalActionDto action, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var entity = await _requests.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException("SponsorshipRequest", requestId);

        if (action.Action == ApprovalDecision.Approve)
            entity.FinanceApprove(userId, action.Remarks, _clock.UtcNow);
        else
            entity.FinanceReject(userId, action.Remarks, _clock.UtcNow);

        await _uow.SaveChangesAsync(ct);
        _cache.Remove(HistoryKey(requestId));
    }

    public async Task<IReadOnlyList<WorkflowHistoryDto>> GetHistoryAsync(Guid requestId, CancellationToken ct = default)
    {
        // Authorize first using a lightweight load; only cache the projected DTOs.
        var meta = await _requests.GetByIdAsync(requestId, ct)
            ?? throw new NotFoundException("SponsorshipRequest", requestId);
        EnsureCanRead(meta);

        var cached = await _cache.GetOrCreateAsync(
            HistoryKey(requestId),
            async c =>
            {
                var entity = await _requests.GetByIdWithHistoryAsync(requestId, c)
                    ?? throw new NotFoundException("SponsorshipRequest", requestId);
                return (IReadOnlyList<WorkflowHistoryDto>)entity.History
                    .OrderBy(h => h.ActionAt)
                    .Select(h => h.ToDto())
                    .ToList();
            },
            HistoryTtl,
            ct);

        return cached ?? Array.Empty<WorkflowHistoryDto>();
    }

    private static string HistoryKey(Guid requestId) => HistoryCachePrefix + requestId;

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
