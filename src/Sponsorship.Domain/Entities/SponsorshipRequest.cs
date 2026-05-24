using Sponsorship.Domain.Common;
using Sponsorship.Domain.Enums;
using Sponsorship.Domain.Exceptions;

namespace Sponsorship.Domain.Entities;

public class SponsorshipRequest : Entity<Guid>
{
    public string Title { get; private set; } = default!;
    public Guid RequestorId { get; private set; }
    public User Requestor { get; private set; } = default!;
    public string Department { get; private set; } = default!;
    public int SponsorshipTypeId { get; private set; }
    public SponsorshipType SponsorshipType { get; private set; } = default!;
    public string EventName { get; private set; } = default!;
    public DateTime EventDate { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public string Purpose { get; private set; } = default!;
    public string? ExpectedBenefit { get; private set; }
    public string? Remarks { get; private set; }
    public RequestStatus Status { get; private set; }
    public string? SupportingDocumentPath { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<WorkflowHistory> _history = new();
    public IReadOnlyCollection<WorkflowHistory> History => _history.AsReadOnly();

    private SponsorshipRequest() { }

    public SponsorshipRequest(Guid id, string title, Guid requestorId, string department,
        int sponsorshipTypeId, string eventName, DateTime eventDate, decimal requestedAmount,
        string purpose, string? expectedBenefit, string? remarks, DateTime createdAt)
    {
        Id = id;
        Title = title;
        RequestorId = requestorId;
        Department = department;
        SponsorshipTypeId = sponsorshipTypeId;
        EventName = eventName;
        EventDate = eventDate;
        RequestedAmount = requestedAmount;
        Purpose = purpose;
        ExpectedBenefit = expectedBenefit;
        Remarks = remarks;
        Status = RequestStatus.Draft;
        CreatedAt = createdAt;
    }

    public void UpdateDraft(string title, string department, int sponsorshipTypeId,
        string eventName, DateTime eventDate, decimal requestedAmount, string purpose,
        string? expectedBenefit, string? remarks, DateTime now)
    {
        if (Status != RequestStatus.Draft)
            throw new DomainException("Only requests in Draft status can be edited.");

        Title = title;
        Department = department;
        SponsorshipTypeId = sponsorshipTypeId;
        EventName = eventName;
        EventDate = eventDate;
        RequestedAmount = requestedAmount;
        Purpose = purpose;
        ExpectedBenefit = expectedBenefit;
        Remarks = remarks;
        UpdatedAt = now;
    }

    public void Submit(Guid actionById, DateTime now)
    {
        if (Status != RequestStatus.Draft)
            throw new InvalidWorkflowTransitionException(Status, WorkflowAction.Submit);
        TransitionTo(RequestStatus.PendingManagerApproval, WorkflowAction.Submit, actionById, null, now);
    }

    public void Cancel(Guid actionById, string? remarks, DateTime now)
    {
        if (Status is not (RequestStatus.Draft or RequestStatus.PendingManagerApproval or RequestStatus.PendingFinanceReview))
            throw new InvalidWorkflowTransitionException(Status, WorkflowAction.Cancel);
        TransitionTo(RequestStatus.Cancelled, WorkflowAction.Cancel, actionById, remarks, now);
    }

    public void ManagerApprove(Guid actionById, string? remarks, DateTime now)
    {
        if (Status != RequestStatus.PendingManagerApproval)
            throw new InvalidWorkflowTransitionException(Status, WorkflowAction.Approve);
        TransitionTo(RequestStatus.PendingFinanceReview, WorkflowAction.Approve, actionById, remarks, now);
    }

    public void ManagerReject(Guid actionById, string? remarks, DateTime now)
    {
        if (Status != RequestStatus.PendingManagerApproval)
            throw new InvalidWorkflowTransitionException(Status, WorkflowAction.Reject);
        TransitionTo(RequestStatus.Rejected, WorkflowAction.Reject, actionById, remarks, now);
    }

    public void FinanceApprove(Guid actionById, string? remarks, DateTime now)
    {
        if (Status != RequestStatus.PendingFinanceReview)
            throw new InvalidWorkflowTransitionException(Status, WorkflowAction.Approve);
        TransitionTo(RequestStatus.Approved, WorkflowAction.Approve, actionById, remarks, now);
    }

    public void FinanceReject(Guid actionById, string? remarks, DateTime now)
    {
        if (Status != RequestStatus.PendingFinanceReview)
            throw new InvalidWorkflowTransitionException(Status, WorkflowAction.Reject);
        TransitionTo(RequestStatus.Rejected, WorkflowAction.Reject, actionById, remarks, now);
    }

    private void TransitionTo(RequestStatus to, WorkflowAction action, Guid actionById,
        string? remarks, DateTime now)
    {
        var from = Status;
        Status = to;
        UpdatedAt = now;
        _history.Add(new WorkflowHistory(Id, actionById, action, from, to, remarks, now));
    }
}
