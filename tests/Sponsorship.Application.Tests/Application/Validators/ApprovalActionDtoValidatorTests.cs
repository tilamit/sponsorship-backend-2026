using FluentValidation.TestHelper;
using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Application.Requests.Validators;
using Xunit;

namespace Sponsorship.Application.Tests.Application.Validators;

public class ApprovalActionDtoValidatorTests
{
    private readonly ApprovalActionDtoValidator _validator = new();

    [Theory]
    [InlineData(ApprovalDecision.Approve)]
    [InlineData(ApprovalDecision.Reject)]
    public void Valid_decision_passes(ApprovalDecision decision)
    {
        _validator.TestValidate(new ApprovalActionDto(decision, "fine"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Null_remarks_is_allowed()
    {
        _validator.TestValidate(new ApprovalActionDto(ApprovalDecision.Approve, null))
            .ShouldNotHaveValidationErrorFor(x => x.Remarks);
    }

    [Fact]
    public void Out_of_range_enum_value_fails()
    {
        var result = _validator.TestValidate(new ApprovalActionDto((ApprovalDecision)999, null));
        result.ShouldHaveValidationErrorFor(x => x.Action);
    }

    [Fact]
    public void Remarks_over_1000_chars_fails()
    {
        var result = _validator.TestValidate(
            new ApprovalActionDto(ApprovalDecision.Reject, new string('r', 1001)));
        result.ShouldHaveValidationErrorFor(x => x.Remarks);
    }
}
