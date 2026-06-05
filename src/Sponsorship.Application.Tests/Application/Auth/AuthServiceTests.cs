using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sponsorship.Application.Auth;
using Sponsorship.Application.Auth.Dtos;
using Sponsorship.Application.Common.Exceptions;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Application.Tests.TestSupport;
using Sponsorship.Domain.Entities;
using Xunit;

namespace Sponsorship.Application.Tests.Application.Auth;

public class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ILoginCountLogger _loginCountLog = Substitute.For<ILoginCountLogger>();
    private readonly FixedDateTimeProvider _clock = FixedDateTimeProvider.Default;
    private readonly JwtSettings _settings = new()
    {
        Issuer = "SponsorshipApi",
        Audience = "SponsorshipClient",
        Key = new string('k', 40),
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    };

    private AuthService CreateSut() =>
        new(_users, _refreshTokens, _hasher, _jwt, _clock, _uow, _loginCountLog, Options.Create(_settings));

    private User ActiveUser(out Role role, bool active = true)
    {
        role = TestData.Role(1, Role.Requestor);
        return TestData.User(role: role, active: active, passwordHash: "stored-hash");
    }

    // ---- Login -------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_with_valid_credentials_issues_tokens_and_persists_refresh()
    {
        var user = ActiveUser(out _);
        _users.GetByEmailAsync("requestor@demo.local", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("Demo@123", "stored-hash").Returns(true);
        _jwt.GenerateAccessToken(user).Returns(("access-jwt", _clock.UtcNow.AddMinutes(15)));
        _jwt.GenerateRefreshToken().Returns("refresh-raw");

        var result = await CreateSut().LoginAsync(new LoginDto("requestor@demo.local", "Demo@123"));

        result.AccessToken.Should().Be("access-jwt");
        result.RefreshToken.Should().Be("refresh-raw");
        result.RefreshTokenExpiresAtUtc.Should().Be(_clock.UtcNow.AddDays(7));
        result.User.Email.Should().Be("requestor@demo.local");
        result.User.Role.Should().Be(Role.Requestor);
        await _refreshTokens.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.Token == "refresh-raw" && t.UserId == user.Id),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_unknown_email_throws_Unauthorized()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = () => CreateSut().LoginAsync(new LoginDto("nobody@demo.local", "Demo@123"));

        await act.Should().ThrowAsync<UnauthorizedException>();
        _hasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_inactive_user_throws_Unauthorized()
    {
        var user = ActiveUser(out _, active: false);
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        var act = () => CreateSut().LoginAsync(new LoginDto("requestor@demo.local", "Demo@123"));

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task LoginAsync_wrong_password_throws_Unauthorized()
    {
        var user = ActiveUser(out _);
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("wrong", "stored-hash").Returns(false);

        var act = () => CreateSut().LoginAsync(new LoginDto("requestor@demo.local", "wrong"));

        await act.Should().ThrowAsync<UnauthorizedException>();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- Refresh -----------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_unknown_token_throws_Unauthorized()
    {
        _refreshTokens.GetByTokenAsync("ghost", Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        var act = () => CreateSut().RefreshAsync("ghost");

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("*not recognized*");
    }

    [Fact]
    public async Task RefreshAsync_revoked_token_triggers_chain_revocation_and_throws()
    {
        var stored = TestData.RefreshToken(token: "reused");
        stored.Revoke(_clock.UtcNow); // already revoked → reuse signal
        _refreshTokens.GetByTokenAsync("reused", Arg.Any<CancellationToken>()).Returns(stored);

        var act = () => CreateSut().RefreshAsync("reused");

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("*reuse detected*");
        await _refreshTokens.Received(1).RevokeAllForUserAsync(
            stored.UserId, _clock.UtcNow, Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_expired_token_throws_Unauthorized()
    {
        var stored = TestData.RefreshToken(token: "old", expiresAt: _clock.UtcNow.AddDays(-1));
        _refreshTokens.GetByTokenAsync("old", Arg.Any<CancellationToken>()).Returns(stored);

        var act = () => CreateSut().RefreshAsync("old");

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("*expired*");
    }

    [Fact]
    public async Task RefreshAsync_when_user_missing_throws_Unauthorized()
    {
        var stored = TestData.RefreshToken(token: "valid", expiresAt: _clock.UtcNow.AddDays(3));
        _refreshTokens.GetByTokenAsync("valid", Arg.Any<CancellationToken>()).Returns(stored);
        _users.GetByIdAsync(stored.UserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = () => CreateSut().RefreshAsync("valid");

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task RefreshAsync_when_user_inactive_throws_Unauthorized()
    {
        var user = ActiveUser(out _, active: false);
        var stored = TestData.RefreshToken(token: "valid", userId: user.Id, expiresAt: _clock.UtcNow.AddDays(3));
        _refreshTokens.GetByTokenAsync("valid", Arg.Any<CancellationToken>()).Returns(stored);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var act = () => CreateSut().RefreshAsync("valid");

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("*inactive*");
    }

    [Fact]
    public async Task RefreshAsync_happy_path_rotates_token_and_returns_new_pair()
    {
        var user = ActiveUser(out _);
        var stored = TestData.RefreshToken(token: "current", userId: user.Id, expiresAt: _clock.UtcNow.AddDays(3));
        _refreshTokens.GetByTokenAsync("current", Arg.Any<CancellationToken>()).Returns(stored);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _jwt.GenerateAccessToken(user).Returns(("new-access", _clock.UtcNow.AddMinutes(15)));
        _jwt.GenerateRefreshToken().Returns("new-refresh");

        var result = await CreateSut().RefreshAsync("current");

        result.AccessToken.Should().Be("new-access");
        result.RefreshToken.Should().Be("new-refresh");
        stored.IsRevoked.Should().BeTrue();
        stored.ReplacedByToken.Should().Be("new-refresh", "old token must point at its successor");
        await _refreshTokens.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.Token == "new-refresh" && t.UserId == user.Id),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- Logout ------------------------------------------------------------

    [Fact]
    public async Task LogoutAsync_unknown_token_is_noop()
    {
        _refreshTokens.GetByTokenAsync("ghost", Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        await CreateSut().LogoutAsync("ghost");

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_active_token_is_revoked_and_saved()
    {
        var stored = TestData.RefreshToken(token: "live");
        _refreshTokens.GetByTokenAsync("live", Arg.Any<CancellationToken>()).Returns(stored);

        await CreateSut().LogoutAsync("live");

        stored.IsRevoked.Should().BeTrue();
        stored.RevokedAt.Should().Be(_clock.UtcNow);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_already_revoked_token_does_not_rerevoke_but_still_saves()
    {
        var stored = TestData.RefreshToken(token: "dead");
        var firstRevoke = _clock.UtcNow.AddDays(-1);
        stored.Revoke(firstRevoke);
        _refreshTokens.GetByTokenAsync("dead", Arg.Any<CancellationToken>()).Returns(stored);

        await CreateSut().LogoutAsync("dead");

        stored.RevokedAt.Should().Be(firstRevoke, "an already-revoked token keeps its original timestamp");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
