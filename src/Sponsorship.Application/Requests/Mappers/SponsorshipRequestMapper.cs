using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Application.Requests.Mappers;

public static class SponsorshipRequestMapper
{
    public static SponsorshipRequestDto ToDto(this SponsorshipRequest r) =>
        new(r.Id, r.Title, r.RequestorId,
            r.Requestor?.FullName ?? string.Empty,
            r.Department, r.SponsorshipTypeId,
            r.SponsorshipType?.Name ?? string.Empty,
            r.EventName, r.EventDate, r.RequestedAmount, r.Purpose,
            r.ExpectedBenefit, r.Remarks,
            r.Status.ToString(), r.CreatedAt, r.UpdatedAt);

    public static WorkflowHistoryDto ToDto(this WorkflowHistory h) =>
        new(h.Id, h.RequestId, h.ActionById,
            h.ActionBy?.FullName ?? string.Empty,
            h.Action.ToString(),
            h.FromStatus.ToString(),
            h.ToStatus.ToString(),
            h.Remarks, h.ActionAt);

    public static SponsorshipTypeDto ToDto(this SponsorshipType t) =>
        new(t.Id, t.Name, t.IsActive);
}
