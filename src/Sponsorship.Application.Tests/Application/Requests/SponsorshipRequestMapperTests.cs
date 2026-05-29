using FluentAssertions;
using Sponsorship.Application.Requests.Mappers;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Domain.Entities;
using Sponsorship.Domain.Enums;
using Xunit;

namespace Sponsorship.Application.Tests.Application.Requests;

public class SponsorshipRequestMapperTests
{
    [Fact]
    public void ToDto_maps_request_with_navigations()
    {
        var requestor = TestData.User(fullName: "Dana Requestor");
        var type = TestData.SponsorshipType(3, "Education");
        var req = TestData.DraftRequest(
            requestorId: requestor.Id,
            title: "Scholarship Fund",
            requestedAmount: 7500m,
            requestor: requestor,
            type: type);

        var dto = req.ToDto();

        dto.Id.Should().Be(req.Id);
        dto.Title.Should().Be("Scholarship Fund");
        dto.RequestorName.Should().Be("Dana Requestor");
        dto.SponsorshipTypeName.Should().Be("Education");
        dto.RequestedAmount.Should().Be(7500m);
        dto.Status.Should().Be(RequestStatus.Draft.ToString());
    }

    [Fact]
    public void ToDto_tolerates_missing_navigations_with_empty_strings()
    {
        // No Requestor / SponsorshipType navigation loaded.
        var req = TestData.DraftRequest();

        var dto = req.ToDto();

        dto.RequestorName.Should().BeEmpty();
        dto.SponsorshipTypeName.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_maps_workflow_history_entry()
    {
        var actor = TestData.User(fullName: "Mike Manager");
        var req = TestData.DraftRequest(requestor: actor);
        req.Submit(actor.Id, TestData.Now);
        var history = req.History.Single();
        TestData.SetNavigation(history, nameof(WorkflowHistory.ActionBy), actor);

        var dto = history.ToDto();

        dto.RequestId.Should().Be(req.Id);
        dto.ActionByName.Should().Be("Mike Manager");
        dto.Action.Should().Be(WorkflowAction.Submit.ToString());
        dto.FromStatus.Should().Be(RequestStatus.Draft.ToString());
        dto.ToStatus.Should().Be(RequestStatus.PendingManagerApproval.ToString());
    }

    [Fact]
    public void ToDto_maps_sponsorship_type()
    {
        var type = TestData.SponsorshipType(9, "Sports", active: false);

        var dto = type.ToDto();

        dto.Id.Should().Be(9);
        dto.Name.Should().Be("Sports");
        dto.IsActive.Should().BeFalse();
    }
}
