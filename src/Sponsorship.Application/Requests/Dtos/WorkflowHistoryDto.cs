namespace Sponsorship.Application.Requests.Dtos;

public record WorkflowHistoryDto(
    long Id,
    Guid RequestId,
    Guid ActionById,
    string ActionByName,
    string Action,
    string FromStatus,
    string ToStatus,
    string? Remarks,
    DateTime ActionAt);
