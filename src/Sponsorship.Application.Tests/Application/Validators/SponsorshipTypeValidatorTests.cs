using FluentValidation.TestHelper;
using Sponsorship.Application.Requests.Dtos;
using Sponsorship.Application.Requests.Validators;
using Xunit;

namespace Sponsorship.Application.Tests.Application.Validators;

public class SponsorshipTypeValidatorTests
{
    private readonly CreateSponsorshipTypeDtoValidator _create = new();
    private readonly UpdateSponsorshipTypeDtoValidator _update = new();

    [Fact]
    public void Create_valid_name_passes()
    {
        _create.TestValidate(new CreateSponsorshipTypeDto("Charity")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_empty_name_fails()
    {
        _create.TestValidate(new CreateSponsorshipTypeDto("")).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Create_name_over_100_chars_fails()
    {
        _create.TestValidate(new CreateSponsorshipTypeDto(new string('n', 101)))
            .ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Update_valid_passes()
    {
        _update.TestValidate(new UpdateSponsorshipTypeDto("Education", true)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_empty_name_fails()
    {
        _update.TestValidate(new UpdateSponsorshipTypeDto("", false)).ShouldHaveValidationErrorFor(x => x.Name);
    }
}
