using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sponsorship.Application.Requests;
using Sponsorship.Application.Requests.Dtos;

namespace Sponsorship.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workflow")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _service;
    private readonly IValidator<ApprovalActionDto> _validator;

    public WorkflowController(IWorkflowService service, IValidator<ApprovalActionDto> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet("pending-manager")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<ActionResult<IReadOnlyList<SponsorshipRequestDto>>> PendingManager(CancellationToken ct)
        => Ok(await _service.ListPendingManagerAsync(ct));

    [HttpGet("pending-finance")]
    [Authorize(Roles = "FinanceAdmin,SystemAdmin")]
    public async Task<ActionResult<IReadOnlyList<SponsorshipRequestDto>>> PendingFinance(CancellationToken ct)
        => Ok(await _service.ListPendingFinanceAsync(ct));

    [HttpPost("{id:guid}/manager-decision")]
    [Authorize(Roles = "Manager")]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> ManagerDecision(Guid id, [FromBody] ApprovalActionDto dto, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(dto, ct);
        await _service.ManagerDecisionAsync(id, dto, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/finance-decision")]
    [Authorize(Roles = "FinanceAdmin")]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> FinanceDecision(Guid id, [FromBody] ApprovalActionDto dto, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(dto, ct);
        await _service.FinanceDecisionAsync(id, dto, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<WorkflowHistoryDto>>> History(Guid id, CancellationToken ct)
        => Ok(await _service.GetHistoryAsync(id, ct));
}
