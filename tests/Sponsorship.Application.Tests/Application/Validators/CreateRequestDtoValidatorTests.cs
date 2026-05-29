using FluentValidation.TestHelper;
using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Application.Requests.Validators;
using Sponsorship.Application.Tests.TestSupport;
using Xunit;

namespace Sponsorship.Application.Tests.Application.Validators;

public class CreateRequestDtoValidatorTests
{
    private readonly FixedDateTimeProvider _clock = FixedDateTimeProvider.Default;
    private readonly CreateRequestDtoValidator _validator;

    public CreateRequestDtoValidatorTests()
    {
        _validator = new CreateRequestDtoValidator(_clock);
    }

    private CreateRequestDto Valid() =>
        new("Conference", "Engineering", 1, "DevWorld",
            _clock.UtcNow.Date.AddDays(10), 1000m, "Purpose text", "Benefit", "Remarks");

    [Fact]
    public void Valid_dto_passes()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Event_date_today_is_allowed()
    {
        var dto = Valid() with { EventDate = _clock.UtcNow.Date };
        _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.EventDate);
    }

    [Fact]
    public void Event_date_in_the_past_fails()
    {
        var dto = Valid() with { EventDate = _clock.UtcNow.Date.AddDays(-1) };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.EventDate);
    }

    [Theory]
    [InlineData("")]
    public void Empty_title_fails(string title)
    {
        var dto = Valid() with { Title = title };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_over_200_chars_fails()
    {
        var dto = Valid() with { Title = new string('t', 201) };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Empty_department_fails()
    {
        var dto = Valid() with { Department = "" };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Department);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Non_positive_sponsorship_type_fails(int typeId)
    {
        var dto = Valid() with { SponsorshipTypeId = typeId };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.SponsorshipTypeId);
    }

    [Fact]
    public void Empty_event_name_fails()
    {
        var dto = Valid() with { EventName = "" };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.EventName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Non_positive_amount_fails(decimal amount)
    {
        var dto = Valid() with { RequestedAmount = amount };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RequestedAmount);
    }

    [Fact]
    public void Amount_with_more_than_two_decimals_fails()
    {
        var dto = Valid() with { RequestedAmount = 100.123m };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RequestedAmount);
    }

    [Fact]
    public void Empty_purpose_fails()
    {
        var dto = Valid() with { Purpose = "" };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Purpose);
    }

    [Fact]
    public void Purpose_over_2000_chars_fails()
    {
        var dto = Valid() with { Purpose = new string('p', 2001) };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Purpose);
    }

    [Fact]
    public void ExpectedBenefit_over_1000_chars_fails()
    {
        var dto = Valid() with { ExpectedBenefit = new string('b', 1001) };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ExpectedBenefit);
    }

    [Fact]
    public void Remarks_over_500_chars_fails()
    {
        var dto = Valid() with { Remarks = new string('r', 501) };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Remarks);
    }

    [Fact]
    public void Null_optional_fields_pass()
    {
        var dto = Valid() with { ExpectedBenefit = null, Remarks = null };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.ExpectedBenefit);
        result.ShouldNotHaveValidationErrorFor(x => x.Remarks);
    }
}
