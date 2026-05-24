using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Sponsorship.Api.Controllers;

[ApiController]
[Route("api/health")]
[DisableRateLimiting]
public class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new { status = "ok", utcNow = DateTime.UtcNow });
}
