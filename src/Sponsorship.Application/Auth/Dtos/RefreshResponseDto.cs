namespace Sponsorship.Application.Auth.Dtos;

public sealed record RefreshResponseDto
{
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpiresAt { get; init; }
    public required DateTime RefreshTokenExpiresAt { get; init; }
}
