using FluentValidation.TestHelper;
using Sponsorship.Application.Auth.Dtos;
using Sponsorship.Application.Auth.Validators;
using Xunit;

namespace Sponsorship.Application.Tests.Application.Validators;

public class LoginDtoValidatorTests
{
    private readonly LoginDtoValidator _validator = new();

    [Fact]
    public void Valid_login_passes()
    {
        var result = _validator.TestValidate(new LoginDto("user@demo.local", "Demo@123"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    public void Invalid_email_fails(string email)
    {
        var result = _validator.TestValidate(new LoginDto(email, "Demo@123"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_over_256_chars_fails()
    {
        var longEmail = new string('a', 250) + "@demo.local";
        var result = _validator.TestValidate(new LoginDto(longEmail, "Demo@123"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Empty_password_fails()
    {
        var result = _validator.TestValidate(new LoginDto("user@demo.local", ""));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_over_200_chars_fails()
    {
        var result = _validator.TestValidate(new LoginDto("user@demo.local", new string('p', 201)));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
