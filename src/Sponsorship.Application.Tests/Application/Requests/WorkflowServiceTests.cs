using FluentAssertions;
using NSubstitute;
using Sponsorship.Application.Common.Exceptions;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Application.Requests;
using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Domain.Entities;
using Sponsorship.Domain.Enums;
using Xunit;

namespace Sponsorship.Application.Tests.Application.Requests;

public class WorkflowServiceTests
{
    private readonly ISponsorshipRequestRepository _requests = Substitute.For<ISponsorshipRequestRepository>();
    private readonly ICurrentUserService _current = Substitute.For<ICurrentUserService>();
    private readonly FixedDateTimeProvider _clock = FixedDateTimeProvider.Default;
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly PassThroughCacheService _cache = new();

    private readonly Guid _managerId = Guid.NewGuid();

    private WorkflowService CreateSut() => new(_requests, _current, _clock, _uow, _cache);

    private void SignInAs(Guid userId, string role)
    {
        _current.UserId.Returns(userId);
        _current.Role.Returns(role);
    }

    private static ApprovalActionDto Approve(string? remarks = "ok") => new(ApprovalDecision.Approve, remarks);
    private static ApprovalActionDto Reject(string? remarks = "no") => new(ApprovalDecision.Reject, remarks);

    private SponsorshipRequest RequestInState(RequestStatus status, Guid? ownerId = null)
    {
        var req = TestData.DraftRequest(requestorId: ownerId ?? Guid.NewGuid());
        var actor = Guid.NewGuid();
        if (status >= RequestStatus.PendingManagerApproval) req.Submit(actor, _clock.UtcNow);
        if (status >= RequestStatus.PendingFinanceReview) req.ManagerApprove(actor, null, _clock.UtcNow);
        if (status == RequestStatus.Approved) req.FinanceApprove(actor, null, _clock.UtcNow);
        return req;
    }

    // ---- Queues ------------------------------------------------------------

    [Fact]
    public async Task ListPendingManagerAsync_returns_requests_in_manager_queue()
    {
        _requests.ListByStatusAsync(RequestStatus.PendingManagerApproval, Arg.Any<CancellationToken>())
            .Returns(new List<SponsorshipRequest> { RequestInState(RequestStatus.PendingManagerApproval) });

        var list = await CreateSut().ListPendingManagerAsync();

        list.Should().ContainSingle();
        await _requests.Received(1).ListByStatusAsync(RequestStatus.PendingManagerApproval, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListPendingFinanceAsync_returns_requests_in_finance_queue()
    {
        _requests.ListByStatusAsync(RequestStatus.PendingFinanceReview, Arg.Any<CancellationToken>())
            .Returns(new List<SponsorshipRequest> { RequestInState(RequestStatus.PendingFinanceReview) });

        var list = await CreateSut().ListPendingFinanceAsync();

        list.Should().ContainSingle();
    }

    // ---- Manager decision --------------------------------------------------

    [Fact]
    public async Task ManagerDecisionAsync_without_user_throws_Unauthorized()
    {
        _current.UserId.Returns((Guid?)null);

        var act = () => CreateSut().ManagerDecisionAsync(Guid.NewGuid(), Approve());

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ManagerDecisionAsync_missing_request_throws_NotFound()
    {
        SignInAs(_managerId, Role.Manager);
        _requests.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SponsorshipRequest?)null);

        var act = () => CreateSut().ManagerDecisionAsync(Guid.NewGuid(), Approve());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ManagerDecisionAsync_approve_advances_to_finance_and_invalidates_history_cache()
    {
        SignInAs(_managerId, Role.Manager);
        var req = RequestInState(RequestStatus.PendingManagerApproval);
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        await CreateSut().ManagerDecisionAsync(req.Id, Approve("approved"));

        req.Status.Should().Be(RequestStatus.PendingFinanceReview);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _cache.Removed.Should().Contain("workflow-history:" + req.Id);
    }

    [Fact]
    public async Task ManagerDecisionAsync_reject_moves_to_rejected()
    {
        SignInAs(_managerId, Role.Manager);
        var req = RequestInState(RequestStatus.PendingManagerApproval);
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        await CreateSut().ManagerDecisionAsync(req.Id, Reject("over budget"));

        req.Status.Should().Be(RequestStatus.Rejected);
    }

    // ---- Finance decision --------------------------------------------------

    [Fact]
    public async Task FinanceDecisionAsync_missing_request_throws_NotFound()
    {
        SignInAs(_managerId, Role.FinanceAdmin);
        _requests.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SponsorshipRequest?)null);

        var act = () => CreateSut().FinanceDecisionAsync(Guid.NewGuid(), Approve());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task FinanceDecisionAsync_approve_moves_to_approved()
    {
        SignInAs(_managerId, Role.FinanceAdmin);
        var req = RequestInState(RequestStatus.PendingFinanceReview);
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        await CreateSut().FinanceDecisionAsync(req.Id, Approve("funded"));

        req.Status.Should().Be(RequestStatus.Approved);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinanceDecisionAsync_reject_moves_to_rejected()
    {
        SignInAs(_managerId, Role.FinanceAdmin);
        var req = RequestInState(RequestStatus.PendingFinanceReview);
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        await CreateSut().FinanceDecisionAsync(req.Id, Reject());

        req.Status.Should().Be(RequestStatus.Rejected);
    }

    // ---- History (read authorization + projection) -------------------------

    [Fact]
    public async Task GetHistoryAsync_missing_request_throws_NotFound()
    {
        SignInAs(_managerId, Role.Manager);
        _requests.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SponsorshipRequest?)null);

        var act = () => CreateSut().GetHistoryAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetHistoryAsync_requestor_cannot_read_others_history()
    {
        var requestor = Guid.NewGuid();
        SignInAs(requestor, Role.Requestor);
        var req = RequestInState(RequestStatus.PendingFinanceReview, ownerId: Guid.NewGuid());
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        var act = () => CreateSut().GetHistoryAsync(req.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetHistoryAsync_owner_gets_ordered_history()
    {
        var requestor = Guid.NewGuid();
        SignInAs(requestor, Role.Requestor);
        var req = RequestInState(RequestStatus.PendingFinanceReview, ownerId: requestor);
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);
        _requests.GetByIdWithHistoryAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        var history = await CreateSut().GetHistoryAsync(req.Id);

        history.Should().HaveCount(2); // Submit + ManagerApprove
        history.Select(h => h.ToStatus).Should().ContainInOrder(
            RequestStatus.PendingManagerApproval.ToString(),
            RequestStatus.PendingFinanceReview.ToString());
        _cache.FactoryInvocations.Should().Be(1, "history is served through the cache layer");
    }

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.FinanceAdmin)]
    [InlineData(Role.SystemAdmin)]
    public async Task GetHistoryAsync_privileged_roles_can_read_any_history(string role)
    {
        SignInAs(Guid.NewGuid(), role);
        var req = RequestInState(RequestStatus.PendingFinanceReview, ownerId: Guid.NewGuid());
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);
        _requests.GetByIdWithHistoryAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        var history = await CreateSut().GetHistoryAsync(req.Id);

        history.Should().NotBeEmpty();
    }
}
