using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sponsorship.Application.Requests;
using Sponsorship.Application.Requests.Dtos;

namespace Sponsorship.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sponsorship-types")]
public class SponsorshipTypesController : ControllerBase
{
    private readonly ISponsorshipTypeService _service;
    private readonly IValidator<CreateSponsorshipTypeDto> _createValidator;
    private readonly IValidator<UpdateSponsorshipTypeDto> _updateValidator;

    public SponsorshipTypesController(
        ISponsorshipTypeService service,
        IValidator<CreateSponsorshipTypeDto> createValidator,
        IValidator<UpdateSponsorshipTypeDto> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SponsorshipTypeDto>>> List(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
        => Ok(await _service.ListAsync(activeOnly, ct));

    [HttpPost]
    [Authorize(Roles = "SystemAdmin")]
    [EnableRateLimiting("writes")]
    public async Task<ActionResult<SponsorshipTypeDto>> Create([FromBody] CreateSponsorshipTypeDto dto, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(dto, ct);
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(List), new { }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SystemAdmin")]
    [EnableRateLimiting("writes")]
    public async Task<ActionResult<SponsorshipTypeDto>> Update(int id, [FromBody] UpdateSponsorshipTypeDto dto, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, ct);
        var updated = await _service.UpdateAsync(id, dto, ct);
        return Ok(updated);
    }
}
