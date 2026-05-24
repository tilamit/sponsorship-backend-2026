namespace Sponsorship.Application.Auth.Dtos;

public sealed record CurrentUserDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public required string Role { get; init; }
}
