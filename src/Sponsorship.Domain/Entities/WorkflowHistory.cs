using Sponsorship.Domain.Common;
using Sponsorship.Domain.Enums;

namespace Sponsorship.Domain.Entities;

public class WorkflowHistory : Entity<long>
{
    public Guid RequestId { get; private set; }
    public Guid ActionById { get; private set; }
    public User ActionBy { get; private set; } = default!;
    public WorkflowAction Action { get; private set; }
    public RequestStatus FromStatus { get; private set; }
    public RequestStatus ToStatus { get; private set; }
    public string? Remarks { get; private set; }
    public DateTime ActionAt { get; private set; }

    private WorkflowHistory() { }

    public WorkflowHistory(Guid requestId, Guid actionById, WorkflowAction action,
        RequestStatus fromStatus, RequestStatus toStatus, string? remarks, DateTime actionAt)
    {
        RequestId = requestId;
        ActionById = actionById;
        Action = action;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Remarks = remarks;
        ActionAt = actionAt;
    }
}
