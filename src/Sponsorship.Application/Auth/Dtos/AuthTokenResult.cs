namespace Sponsorship.Application.Auth.Dtos;

// Application-layer result that carries the raw refresh token alongside
// what the controller needs to build the public response. The controller
// uses RefreshToken to set the httpOnly cookie and MUST NOT include it in
// the JSON body — that's enforced by the LoginResponseDto / RefreshResponseDto
// types not having a refresh-token field.
public sealed record AuthTokenResult
{
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpiresAtUtc { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime RefreshTokenExpiresAtUtc { get; init; }
    public required CurrentUserDto User { get; init; }
}
