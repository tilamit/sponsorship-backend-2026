using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sponsorship.Application.Requests;
using Sponsorship.Application.Requests.Dtos;

namespace Sponsorship.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sponsorship-requests")]
public class SponsorshipRequestsController : ControllerBase
{
    private readonly ISponsorshipRequestService _service;
    private readonly IValidator<CreateRequestDto> _createValidator;
    private readonly IValidator<UpdateRequestDto> _updateValidator;

    public SponsorshipRequestsController(
        ISponsorshipRequestService service,
        IValidator<CreateRequestDto> createValidator,
        IValidator<UpdateRequestDto> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SponsorshipRequestDto>>> List(CancellationToken ct)
        => Ok(await _service.ListForCurrentUserAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SponsorshipRequestDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = "Requestor")]
    [EnableRateLimiting("writes")]
    public async Task<ActionResult<SponsorshipRequestDto>> Create([FromBody] CreateRequestDto dto, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(dto, ct);
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Requestor")]
    [EnableRateLimiting("writes")]
    public async Task<ActionResult<SponsorshipRequestDto>> Update(Guid id, [FromBody] UpdateRequestDto dto, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, ct);
        var updated = await _service.UpdateAsync(id, dto, ct);
        return Ok(updated);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Requestor")]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        await _service.SubmitAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Requestor")]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelRequestDto? dto, CancellationToken ct)
    {
        await _service.CancelAsync(id, dto?.Remarks, ct);
        return NoContent();
    }
}
