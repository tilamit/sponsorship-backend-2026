using FluentValidation.TestHelper;
using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Application.Requests.Validators;
using Sponsorship.Application.Tests.TestSupport;
using Xunit;

namespace Sponsorship.Application.Tests.Application.Validators;

public class UpdateRequestDtoValidatorTests
{
    private readonly FixedDateTimeProvider _clock = FixedDateTimeProvider.Default;
    private readonly UpdateRequestDtoValidator _validator;

    public UpdateRequestDtoValidatorTests()
    {
        _validator = new UpdateRequestDtoValidator(_clock);
    }

    private UpdateRequestDto Valid() =>
        new("Conference", "Engineering", 1, "DevWorld",
            _clock.UtcNow.Date.AddDays(10), 1000m, "Purpose text", null, null);

    [Fact]
    public void Valid_dto_passes()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Past_event_date_fails()
    {
        var dto = Valid() with { EventDate = _clock.UtcNow.Date.AddDays(-1) };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.EventDate);
    }

    [Fact]
    public void Empty_title_fails()
    {
        var dto = Valid() with { Title = "" };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Non_positive_amount_fails()
    {
        var dto = Valid() with { RequestedAmount = 0m };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RequestedAmount);
    }

    [Fact]
    public void Amount_with_three_decimals_fails()
    {
        var dto = Valid() with { RequestedAmount = 12.345m };
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RequestedAmount);
    }
}
