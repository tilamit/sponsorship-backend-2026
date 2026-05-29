using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Sponsorship.Application.Auth;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Domain.Entities;
using Sponsorship.Infrastructure.Identity;
using Xunit;

namespace Sponsorship.Application.Tests.Infrastructure.Identity;

public class JwtTokenServiceTests
{
    private readonly FixedDateTimeProvider _clock = FixedDateTimeProvider.Default;

    private static JwtSettings Settings(string? key = null, string issuer = "SponsorshipApi", string audience = "SponsorshipClient")
        => new()
        {
            Issuer = issuer,
            Audience = audience,
            Key = key ?? new string('k', 40),
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        };

    private JwtTokenService CreateSut(JwtSettings? settings = null)
        => new(Options.Create(settings ?? Settings()), _clock);

    private User TestUser()
    {
        var role = TestData.Role(2, Role.Manager);
        return TestData.User(email: "manager@demo.local", fullName: "Mona Manager", role: role);
    }

    [Fact]
    public void GenerateAccessToken_sets_expiry_from_settings()
    {
        var (_, expires) = CreateSut().GenerateAccessToken(TestUser());

        expires.Should().Be(_clock.UtcNow.AddMinutes(15));
    }

    [Fact]
    public void GenerateAccessToken_embeds_user_claims()
    {
        var user = TestUser();

        var (token, _) = CreateSut().GenerateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == Role.Manager);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "Mona Manager");
        jwt.Issuer.Should().Be("SponsorshipApi");
        jwt.Audiences.Should().Contain("SponsorshipClient");
    }

    [Fact]
    public void GenerateRefreshToken_is_unique_and_decodes_to_64_bytes()
    {
        var sut = CreateSut();

        var a = sut.GenerateRefreshToken();
        var b = sut.GenerateRefreshToken();

        a.Should().NotBe(b);
        Convert.FromBase64String(a).Should().HaveCount(64);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_returns_principal_for_valid_signature()
    {
        var sut = CreateSut();
        var user = TestUser();
        var (token, _) = sut.GenerateAccessToken(user);

        var principal = sut.GetPrincipalFromExpiredToken(token);

        principal.Should().NotBeNull();
        principal!.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            .Should().Be(user.Id.ToString());
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_rejects_garbage()
    {
        CreateSut().GetPrincipalFromExpiredToken("not.a.jwt").Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_rejects_token_signed_with_a_different_key()
    {
        var foreign = new JwtTokenService(Options.Create(Settings(key: new string('x', 40))), _clock);
        var (token, _) = foreign.GenerateAccessToken(TestUser());

        // Validated by a service using the canonical key — signature won't match.
        CreateSut().GetPrincipalFromExpiredToken(token).Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_rejects_wrong_issuer()
    {
        var foreign = new JwtTokenService(Options.Create(Settings(issuer: "EvilIssuer")), _clock);
        var (token, _) = foreign.GenerateAccessToken(TestUser());

        CreateSut().GetPrincipalFromExpiredToken(token).Should().BeNull();
    }
}
