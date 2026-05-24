namespace Sponsorship.Application.Requests.Dtos;

public enum ApprovalDecision
{
    Approve = 1,
    Reject = 2
}

public record ApprovalActionDto(ApprovalDecision Action, string? Remarks);

public record CancelRequestDto(string? Remarks);
