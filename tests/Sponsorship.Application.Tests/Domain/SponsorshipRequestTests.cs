using FluentAssertions;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Domain.Enums;
using Sponsorship.Domain.Exceptions;
using Xunit;

namespace Sponsorship.Application.Tests.Domain;

/// Exercises the SponsorshipRequest aggregate's workflow state machine — the heart
/// of the business logic. Every legal transition is asserted, and every illegal one
/// must throw without mutating state.
public class SponsorshipRequestTests
{
    private static readonly DateTime Now = TestData.Now;
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Constructor_starts_in_Draft_with_all_fields_set()
    {
        var req = TestData.DraftRequest(requestedAmount: 1234.56m);

        req.Status.Should().Be(RequestStatus.Draft);
        req.RequestedAmount.Should().Be(1234.56m);
        req.CreatedAt.Should().Be(Now);
        req.UpdatedAt.Should().BeNull();
        req.History.Should().BeEmpty();
    }

    // ---- UpdateDraft -------------------------------------------------------

    [Fact]
    public void UpdateDraft_in_Draft_updates_fields_and_stamps_UpdatedAt()
    {
        var req = TestData.DraftRequest();
        var later = Now.AddHours(1);

        req.UpdateDraft("New Title", "Finance", 2, "New Event",
            Now.Date.AddDays(10), 999m, "Updated purpose", "benefit", "remarks", later);

        req.Title.Should().Be("New Title");
        req.Department.Should().Be("Finance");
        req.SponsorshipTypeId.Should().Be(2);
        req.RequestedAmount.Should().Be(999m);
        req.UpdatedAt.Should().Be(later);
        req.Status.Should().Be(RequestStatus.Draft);
    }

    [Fact]
    public void UpdateDraft_when_not_Draft_throws_DomainException()
    {
        var req = TestData.DraftRequest();
        req.Submit(Actor, Now); // now PendingManagerApproval

        var act = () => req.UpdateDraft("x", "y", 1, "z",
            Now.Date.AddDays(5), 1m, "p", null, null, Now);

        act.Should().Throw<DomainException>()
            .WithMessage("*Draft*");
    }

    // ---- Submit ------------------------------------------------------------

    [Fact]
    public void Submit_from_Draft_moves_to_PendingManagerApproval_and_records_history()
    {
        var req = TestData.DraftRequest();

        req.Submit(Actor, Now);

        req.Status.Should().Be(RequestStatus.PendingManagerApproval);
        req.UpdatedAt.Should().Be(Now);
        req.History.Should().ContainSingle();
        var h = req.History.Single();
        h.Action.Should().Be(WorkflowAction.Submit);
        h.FromStatus.Should().Be(RequestStatus.Draft);
        h.ToStatus.Should().Be(RequestStatus.PendingManagerApproval);
        h.ActionById.Should().Be(Actor);
        h.ActionAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(RequestStatus.PendingManagerApproval)]
    [InlineData(RequestStatus.PendingFinanceReview)]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.Cancelled)]
    public void Submit_from_non_Draft_throws(RequestStatus current)
    {
        var req = MoveTo(current);

        var act = () => req.Submit(Actor, Now);

        act.Should().Throw<InvalidWorkflowTransitionException>();
    }

    // ---- Cancel ------------------------------------------------------------

    [Theory]
    [InlineData(RequestStatus.Draft)]
    [InlineData(RequestStatus.PendingManagerApproval)]
    [InlineData(RequestStatus.PendingFinanceReview)]
    public void Cancel_is_allowed_before_terminal_states(RequestStatus current)
    {
        var req = MoveTo(current);

        req.Cancel(Actor, "no longer needed", Now);

        req.Status.Should().Be(RequestStatus.Cancelled);
        req.History.Last().Action.Should().Be(WorkflowAction.Cancel);
        req.History.Last().Remarks.Should().Be("no longer needed");
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.Cancelled)]
    public void Cancel_from_terminal_state_throws(RequestStatus current)
    {
        var req = MoveTo(current);

        var act = () => req.Cancel(Actor, null, Now);

        act.Should().Throw<InvalidWorkflowTransitionException>();
    }

    // ---- Manager decision --------------------------------------------------

    [Fact]
    public void ManagerApprove_moves_PendingManager_to_PendingFinance()
    {
        var req = MoveTo(RequestStatus.PendingManagerApproval);

        req.ManagerApprove(Actor, "looks good", Now);

        req.Status.Should().Be(RequestStatus.PendingFinanceReview);
        req.History.Last().Action.Should().Be(WorkflowAction.Approve);
        req.History.Last().FromStatus.Should().Be(RequestStatus.PendingManagerApproval);
    }

    [Fact]
    public void ManagerReject_moves_PendingManager_to_Rejected()
    {
        var req = MoveTo(RequestStatus.PendingManagerApproval);

        req.ManagerReject(Actor, "over budget", Now);

        req.Status.Should().Be(RequestStatus.Rejected);
        req.History.Last().Action.Should().Be(WorkflowAction.Reject);
    }

    [Theory]
    [InlineData(RequestStatus.Draft)]
    [InlineData(RequestStatus.PendingFinanceReview)]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.Cancelled)]
    public void ManagerApprove_from_wrong_state_throws(RequestStatus current)
    {
        var req = MoveTo(current);
        var act = () => req.ManagerApprove(Actor, null, Now);
        act.Should().Throw<InvalidWorkflowTransitionException>();
    }

    [Theory]
    [InlineData(RequestStatus.Draft)]
    [InlineData(RequestStatus.PendingFinanceReview)]
    [InlineData(RequestStatus.Approved)]
    public void ManagerReject_from_wrong_state_throws(RequestStatus current)
    {
        var req = MoveTo(current);
        var act = () => req.ManagerReject(Actor, null, Now);
        act.Should().Throw<InvalidWorkflowTransitionException>();
    }

    // ---- Finance decision --------------------------------------------------

    [Fact]
    public void FinanceApprove_moves_PendingFinance_to_Approved()
    {
        var req = MoveTo(RequestStatus.PendingFinanceReview);

        req.FinanceApprove(Actor, "approved", Now);

        req.Status.Should().Be(RequestStatus.Approved);
        req.History.Last().ToStatus.Should().Be(RequestStatus.Approved);
    }

    [Fact]
    public void FinanceReject_moves_PendingFinance_to_Rejected()
    {
        var req = MoveTo(RequestStatus.PendingFinanceReview);

        req.FinanceReject(Actor, "insufficient funds", Now);

        req.Status.Should().Be(RequestStatus.Rejected);
    }

    [Theory]
    [InlineData(RequestStatus.Draft)]
    [InlineData(RequestStatus.PendingManagerApproval)]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.Cancelled)]
    public void FinanceApprove_from_wrong_state_throws(RequestStatus current)
    {
        var req = MoveTo(current);
        var act = () => req.FinanceApprove(Actor, null, Now);
        act.Should().Throw<InvalidWorkflowTransitionException>();
    }

    // ---- Full happy path ---------------------------------------------------

    [Fact]
    public void Full_approval_path_accumulates_ordered_history()
    {
        var req = TestData.DraftRequest();

        req.Submit(Actor, Now);
        req.ManagerApprove(Actor, "ok", Now.AddHours(1));
        req.FinanceApprove(Actor, "funded", Now.AddHours(2));

        req.Status.Should().Be(RequestStatus.Approved);
        req.History.Should().HaveCount(3);
        req.History.Select(h => h.ToStatus).Should().ContainInOrder(
            RequestStatus.PendingManagerApproval,
            RequestStatus.PendingFinanceReview,
            RequestStatus.Approved);
    }

    /// Drives a fresh Draft request through legal transitions until it reaches
    /// the requested status, so the "wrong-state" tests start from a real state.
    private static Sponsorship.Domain.Entities.SponsorshipRequest MoveTo(RequestStatus target)
    {
        var req = TestData.DraftRequest();
        switch (target)
        {
            case RequestStatus.Draft:
                break;
            case RequestStatus.PendingManagerApproval:
                req.Submit(Actor, Now);
                break;
            case RequestStatus.PendingFinanceReview:
                req.Submit(Actor, Now);
                req.ManagerApprove(Actor, null, Now);
                break;
            case RequestStatus.Approved:
                req.Submit(Actor, Now);
                req.ManagerApprove(Actor, null, Now);
                req.FinanceApprove(Actor, null, Now);
                break;
            case RequestStatus.Rejected:
                req.Submit(Actor, Now);
                req.ManagerReject(Actor, null, Now);
                break;
            case RequestStatus.Cancelled:
                req.Cancel(Actor, null, Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
        return req;
    }
}
