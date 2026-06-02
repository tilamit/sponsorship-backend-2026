using FluentAssertions;
using NSubstitute;
using Sponsorship.Application.Common.Exceptions;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Application.Requests;
using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Domain.Entities;
using Sponsorship.Domain.Exceptions;
using Xunit;

namespace Sponsorship.Application.Tests.Application.Requests;

public class SponsorshipRequestServiceTests
{
    private readonly ISponsorshipRequestRepository _requests = Substitute.For<ISponsorshipRequestRepository>();
    private readonly ISponsorshipTypeRepository _types = Substitute.For<ISponsorshipTypeRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ICurrentUserService _current = Substitute.For<ICurrentUserService>();
    private readonly FixedDateTimeProvider _clock = FixedDateTimeProvider.Default;
    private readonly IUnitOfWork _uow = UnitOfWorkSubstitute.Create();

    private readonly Guid _userId = Guid.NewGuid();

    private SponsorshipRequestService CreateSut() =>
        new(_requests, _types, _users, _current, _clock, _uow);

    private void SignInAs(Guid userId, string role = Role.Requestor)
    {
        _current.UserId.Returns(userId);
        _current.Role.Returns(role);
    }

    private CreateRequestDto ValidCreateDto(int typeId = 1) =>
        new("Title", "Engineering", typeId, "Event",
            _clock.UtcNow.Date.AddDays(10), 1000m, "Purpose", "Benefit", "Remarks");

    private UpdateRequestDto ValidUpdateDto(int typeId = 1) =>
        new("New Title", "Finance", typeId, "New Event",
            _clock.UtcNow.Date.AddDays(15), 2000m, "New Purpose", null, null);

    // ---- Create ------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_without_authenticated_user_throws_Unauthorized()
    {
        _current.UserId.Returns((Guid?)null);

        var act = () => CreateSut().CreateAsync(ValidCreateDto());

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task CreateAsync_when_user_missing_throws_NotFound()
    {
        SignInAs(_userId);
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = () => CreateSut().CreateAsync(ValidCreateDto());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_when_type_missing_throws_NotFound()
    {
        SignInAs(_userId);
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(TestData.User(id: _userId));
        _types.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns((SponsorshipType?)null);

        var act = () => CreateSut().CreateAsync(ValidCreateDto());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_when_type_inactive_throws_DomainException()
    {
        SignInAs(_userId);
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(TestData.User(id: _userId));
        _types.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestData.SponsorshipType(active: false));

        var act = () => CreateSut().CreateAsync(ValidCreateDto());

        await act.Should().ThrowAsync<DomainException>().WithMessage("*inactive*");
    }

    [Fact]
    public async Task CreateAsync_persists_request_owned_by_current_user_and_returns_dto()
    {
        SignInAs(_userId);
        var user = TestData.User(id: _userId);
        var type = TestData.SponsorshipType();
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);
        _types.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(type);

        SponsorshipRequest? added = null;
        await _requests.AddAsync(Arg.Do<SponsorshipRequest>(r => added = r), Arg.Any<CancellationToken>());
        // After save, the service reloads by id — return whatever was added.
        _requests.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => added);

        var dto = await CreateSut().CreateAsync(ValidCreateDto());

        added.Should().NotBeNull();
        added!.RequestorId.Should().Be(_userId, "the requestor is taken from the auth context, never the DTO");
        dto.Title.Should().Be("Title");
        await _requests.Received(1).AddAsync(Arg.Any<SponsorshipRequest>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- Update ------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_when_request_missing_throws_NotFound()
    {
        SignInAs(_userId);
        _requests.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SponsorshipRequest?)null);

        var act = () => CreateSut().UpdateAsync(Guid.NewGuid(), ValidUpdateDto());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_by_non_owner_throws_Forbidden()
    {
        SignInAs(_userId);
        var othersRequest = TestData.DraftRequest(requestorId: Guid.NewGuid());
        _requests.GetByIdAsync(othersRequest.Id, Arg.Any<CancellationToken>()).Returns(othersRequest);

        var act = () => CreateSut().UpdateAsync(othersRequest.Id, ValidUpdateDto());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAsync_with_inactive_type_throws_DomainException()
    {
        SignInAs(_userId);
        var req = TestData.DraftRequest(requestorId: _userId);
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);
        _types.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestData.SponsorshipType(active: false));

        var act = () => CreateSut().UpdateAsync(req.Id, ValidUpdateDto());

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task UpdateAsync_on_non_draft_request_throws_DomainException()
    {
        SignInAs(_userId);
        var req = TestData.DraftRequest(requestorId: _userId);
        req.Submit(_userId, _clock.UtcNow); // no longer Draft
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);
        _types.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestData.SponsorshipType());

        var act = () => CreateSut().UpdateAsync(req.Id, ValidUpdateDto());

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Draft*");
    }

    [Fact]
    public async Task UpdateAsync_happy_path_updates_and_saves()
    {
        SignInAs(_userId);
        var req = TestData.DraftRequest(requestorId: _userId, title: "Old");
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);
        _types.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestData.SponsorshipType());

        var dto = await CreateSut().UpdateAsync(req.Id, ValidUpdateDto());

        req.Title.Should().Be("New Title");
        req.UpdatedAt.Should().Be(_clock.UtcNow);
        dto.Title.Should().Be("New Title");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- GetById (read authorization) -------------------------------------

    [Fact]
    public async Task GetByIdAsync_missing_throws_NotFound()
    {
        SignInAs(_userId);
        _requests.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SponsorshipRequest?)null);

        var act = () => CreateSut().GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_requestor_cannot_read_someone_elses_request()
    {
        SignInAs(_userId, Role.Requestor);
        var othersRequest = TestData.DraftRequest(requestorId: Guid.NewGuid());
        _requests.GetByIdAsync(othersRequest.Id, Arg.Any<CancellationToken>()).Returns(othersRequest);

        var act = () => CreateSut().GetByIdAsync(othersRequest.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetByIdAsync_requestor_can_read_own_request()
    {
        SignInAs(_userId, Role.Requestor);
        var req = TestData.DraftRequest(requestorId: _userId);
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        var dto = await CreateSut().GetByIdAsync(req.Id);

        dto.Id.Should().Be(req.Id);
    }

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.FinanceAdmin)]
    [InlineData(Role.SystemAdmin)]
    public async Task GetByIdAsync_privileged_roles_can_read_any_request(string role)
    {
        SignInAs(_userId, role);
        var req = TestData.DraftRequest(requestorId: Guid.NewGuid());
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        var dto = await CreateSut().GetByIdAsync(req.Id);

        dto.Id.Should().Be(req.Id);
    }

    // ---- List (role-scoped) -----------------------------------------------

    [Fact]
    public async Task ListForCurrentUserAsync_requestor_sees_only_own()
    {
        SignInAs(_userId, Role.Requestor);
        _requests.ListByRequestorAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<SponsorshipRequest> { TestData.DraftRequest(requestorId: _userId) });

        var list = await CreateSut().ListForCurrentUserAsync();

        list.Should().ContainSingle();
        await _requests.Received(1).ListByRequestorAsync(_userId, Arg.Any<CancellationToken>());
        await _requests.DidNotReceive().ListAllAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.FinanceAdmin)]
    [InlineData(Role.SystemAdmin)]
    public async Task ListForCurrentUserAsync_privileged_roles_see_all(string role)
    {
        SignInAs(_userId, role);
        _requests.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SponsorshipRequest> { TestData.DraftRequest(), TestData.DraftRequest() });

        var list = await CreateSut().ListForCurrentUserAsync();

        list.Should().HaveCount(2);
        await _requests.Received(1).ListAllAsync(Arg.Any<CancellationToken>());
        await _requests.DidNotReceive().ListByRequestorAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ---- Submit ------------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_by_non_owner_throws_Forbidden()
    {
        SignInAs(_userId);
        var req = TestData.DraftRequest(requestorId: Guid.NewGuid());
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        var act = () => CreateSut().SubmitAsync(req.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task SubmitAsync_owner_transitions_and_saves()
    {
        SignInAs(_userId);
        var req = TestData.DraftRequest(requestorId: _userId);
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        await CreateSut().SubmitAsync(req.Id);

        req.Status.Should().Be(Sponsorship.Domain.Enums.RequestStatus.PendingManagerApproval);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- Cancel ------------------------------------------------------------

    [Fact]
    public async Task CancelAsync_by_non_owner_throws_Forbidden()
    {
        SignInAs(_userId);
        var req = TestData.DraftRequest(requestorId: Guid.NewGuid());
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        var act = () => CreateSut().CancelAsync(req.Id, "stop");

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CancelAsync_owner_cancels_and_saves()
    {
        SignInAs(_userId);
        var req = TestData.DraftRequest(requestorId: _userId);
        _requests.GetByIdAsync(req.Id, Arg.Any<CancellationToken>()).Returns(req);

        await CreateSut().CancelAsync(req.Id, "changed my mind");

        req.Status.Should().Be(Sponsorship.Domain.Enums.RequestStatus.Cancelled);
        req.History.Last().Remarks.Should().Be("changed my mind");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
