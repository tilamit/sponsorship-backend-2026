using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sponsorship.Application.Auth;
using Sponsorship.Application.Auth.Dtos;
using Sponsorship.Application.Auth.Validators;

namespace Sponsorship.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const string RefreshCookie = "refreshToken";
    private const string CookiePath = "/api/auth";

    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        await new LoginDtoValidator().ValidateAndThrowAsync(dto, ct);
        var result = await _auth.LoginAsync(dto, ct);
        AppendRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);
        return Ok(new LoginResponseDto
        {
            AccessToken = result.AccessToken,
            AccessTokenExpiresAt = result.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAt = result.RefreshTokenExpiresAtUtc,
            User = result.User
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<RefreshResponseDto>> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new { error = "Missing refresh token" });
        }

        var result = await _auth.RefreshAsync(refreshToken, ct);
        AppendRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);
        return Ok(new RefreshResponseDto
        {
            AccessToken = result.AccessToken,
            AccessTokenExpiresAt = result.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAt = result.RefreshTokenExpiresAtUtc
        });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            await _auth.LogoutAsync(refreshToken, ct);
        }
        Response.Cookies.Delete(RefreshCookie, new CookieOptions
        {
            Path = CookiePath,
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Strict
        });
        return NoContent();
    }

    private void AppendRefreshCookie(string refreshToken, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(RefreshCookie, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            // Secure cookies require HTTPS. In Development we serve the API over
            // https://localhost:7225 so the dev cert satisfies this.
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero),
            Path = CookiePath,
            IsEssential = true
        });
    }
}
