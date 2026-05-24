using Sponsorship.Application.Requests.Dtos;

namespace Sponsorship.Application.Requests;

public interface IWorkflowService
{
    Task<IReadOnlyList<SponsorshipRequestDto>> ListPendingManagerAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SponsorshipRequestDto>> ListPendingFinanceAsync(CancellationToken ct = default);
    Task ManagerDecisionAsync(Guid requestId, ApprovalActionDto action, CancellationToken ct = default);
    Task FinanceDecisionAsync(Guid requestId, ApprovalActionDto action, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowHistoryDto>> GetHistoryAsync(Guid requestId, CancellationToken ct = default);
}
