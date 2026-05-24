using Sponsorship.Application.Auth.Dtos;

namespace Sponsorship.Application.Auth;

public interface IAuthService
{
    Task<AuthTokenResult> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<AuthTokenResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
