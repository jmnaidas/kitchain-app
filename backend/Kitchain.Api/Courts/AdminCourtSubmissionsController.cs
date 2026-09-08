using System.Data.Common;
using Kitchain.Application.Courts;
using Kitchain.Domain.Courts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Api.Courts;

// Portfolio admin preview: these endpoints intentionally have no authentication in Phase 2F.
[ApiController]
[Route("api/admin/court-submissions")]
public sealed class AdminCourtSubmissionsController(ICourtSubmissionModeration moderation,
    ILogger<AdminCourtSubmissionsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CourtSubmissionSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourtSubmissionSummary>>> List(CancellationToken cancellationToken,
        [FromQuery] CourtSubmissionStatus status = CourtSubmissionStatus.Pending)
    {
        if (!Enum.IsDefined(status))
        {
            ModelState.AddModelError("status", "Use Pending, Approved or Rejected.");
            return ValidationProblem(ModelState);
        }
        return Ok(await moderation.ListAsync(status, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CourtSubmissionReview>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtSubmissionReview>> Find(Guid id, CancellationToken cancellationToken)
    {
        var submission = await moderation.FindAsync(id, cancellationToken);
        return submission is null ? NotFound() : Ok(submission);
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<CourtModerationReceipt>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<CourtModerationReceipt>> Approve(Guid id, CancellationToken cancellationToken) =>
        Decide(id, CourtSubmissionDecision.Approve, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<CourtModerationReceipt>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<CourtModerationReceipt>> Reject(Guid id, CancellationToken cancellationToken) =>
        Decide(id, CourtSubmissionDecision.Reject, cancellationToken);

    private async Task<ActionResult<CourtModerationReceipt>> Decide(Guid id, CourtSubmissionDecision decision, CancellationToken cancellationToken)
    {
        try
        {
            var result = await moderation.DecideAsync(id, decision, cancellationToken);
            return result.Outcome switch
            {
                CourtModerationOutcome.Success => Ok(result.Receipt),
                CourtModerationOutcome.NotFound => NotFound(),
                CourtModerationOutcome.Conflict => Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "This submission has already been processed. Reload to see its current status."),
                _ => Problem(statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "This submission does not meet the Court validation rules. No court was published.")
            };
        }
        catch (Exception error) when (error is DbUpdateException or DbException)
        {
            logger.LogError("Failed to persist moderation decision for submission {SubmissionId}.", id);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "We could not confirm this decision. Reload the submission before trying again.");
        }
    }
}
