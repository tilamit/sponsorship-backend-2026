using System.Security.Claims;
using Sponsorship.Application.Common.Interfaces;

namespace Sponsorship.Api.Services;

public class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;
    public HttpCurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var raw = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? Principal?.FindFirst("sub")?.Value;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value;
    public string? Role => Principal?.FindFirst(ClaimTypes.Role)?.Value;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
}
