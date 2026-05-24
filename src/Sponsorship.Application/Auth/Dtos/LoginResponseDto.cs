namespace Sponsorship.Application.Auth.Dtos;

public sealed record LoginResponseDto
{
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpiresAt { get; init; }   // UTC
    public required DateTime RefreshTokenExpiresAt { get; init; }  // UTC, NO refresh token itself
    public required CurrentUserDto User { get; init; }
}
