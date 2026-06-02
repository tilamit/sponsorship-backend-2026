using Microsoft.Extensions.Options;
using Sponsorship.Application.Auth.Dtos;
using Sponsorship.Application.Common.Exceptions;
using Sponsorship.Application.Common.Interfaces;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly JwtSettings _settings;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IOptions<JwtSettings> settings)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _hasher = hasher;
        _jwt = jwt;
        _clock = clock;
        _uow = uow;
        _settings = settings.Value;
    }

    public async Task<AuthTokenResult> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(dto.Email, ct);

        //if (user is null || !user.IsActive)
            if (user is null || !user.IsActive || !_hasher.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthTokenResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await _refreshTokens.GetByTokenAsync(refreshToken, ct)
            ?? throw new UnauthorizedException("Refresh token not recognized.");

        var now = _clock.UtcNow;

        if (existing.IsRevoked)
        {
            // Reuse detected — revoke the whole chain for this user.
            await _refreshTokens.RevokeAllForUserAsync(existing.UserId, now, ct);
            await _uow.SaveChangesAsync(ct);
            throw new UnauthorizedException("Refresh token reuse detected. Please log in again.");
        }

        if (existing.IsExpired(now))
            throw new UnauthorizedException("Refresh token expired.");

        var user = await _users.GetByIdAsync(existing.UserId, ct)
            ?? throw new UnauthorizedException("User not found.");
        if (!user.IsActive)
            throw new UnauthorizedException("User is inactive.");

        var (accessToken, accessExpires) = _jwt.GenerateAccessToken(user);
        var newRefreshValue = _jwt.GenerateRefreshToken();
        var refreshExpires = now.AddDays(_settings.RefreshTokenDays);
        var newRefresh = new RefreshToken(
            Guid.NewGuid(), user.Id, newRefreshValue, refreshExpires, now);

        existing.Revoke(now, newRefreshValue);
        await _refreshTokens.AddAsync(newRefresh, ct);
        await _uow.SaveChangesAsync(ct);

        return BuildResult(user, accessToken, accessExpires, newRefreshValue, refreshExpires);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await _refreshTokens.GetByTokenAsync(refreshToken, ct);
        if (token is null) return;
        if (!token.IsRevoked) token.Revoke(_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<AuthTokenResult> IssueTokensAsync(User user, CancellationToken ct)
    {
        var (accessToken, accessExpires) = _jwt.GenerateAccessToken(user);
        var refreshValue = _jwt.GenerateRefreshToken();
        var now = _clock.UtcNow;
        var refreshExpires = now.AddDays(_settings.RefreshTokenDays);
        var refresh = new RefreshToken(Guid.NewGuid(), user.Id, refreshValue, refreshExpires, now);
        await _refreshTokens.AddAsync(refresh, ct);
        await _uow.SaveChangesAsync(ct);
        return BuildResult(user, accessToken, accessExpires, refreshValue, refreshExpires);
    }

    private static AuthTokenResult BuildResult(
        User user, string accessToken, DateTime accessExpires,
        string refreshToken, DateTime refreshExpires) => new()
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessExpires,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshExpires,
            User = new CurrentUserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role.Name
            }
        };
}
