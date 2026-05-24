using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sponsorship.Application.Common.Exceptions;
using Sponsorship.Application.Common.Interfaces;

namespace Sponsorship.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private static readonly TimeSpan MeTtl = TimeSpan.FromMinutes(2);

    private readonly ICurrentUserService _current;
    private readonly IUserRepository _users;
    private readonly ICacheService _cache;

    public UsersController(ICurrentUserService current, IUserRepository users, ICacheService cache)
    {
        _current = current;
        _users = users;
        _cache = cache;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (_current.UserId is null) throw new UnauthorizedException("No user context.");
        var id = _current.UserId.Value;

        var dto = await _cache.GetOrCreateAsync(
            $"users:me:{id}",
            async c =>
            {
                var user = await _users.GetByIdAsync(id, c)
                    ?? throw new NotFoundException("User", id);
                return new MeDto(user.Id, user.Email, user.FullName, user.Department, user.Role.Name);
            },
            MeTtl,
            ct);

        return Ok(dto);
    }

    private sealed record MeDto(Guid Id, string Email, string FullName, string? Department, string Role);
}
