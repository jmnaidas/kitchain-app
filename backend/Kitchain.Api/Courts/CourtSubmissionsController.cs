using Kitchain.Application.Courts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Api.Courts;

[ApiController]
[Route("api/court-submissions")]
public sealed class CourtSubmissionsController(CourtSubmissionService submissions, ILogger<CourtSubmissionsController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(65536)]
    [ProducesResponseType<CourtSubmissionReceipt>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourtSubmissionReceipt>> Create(CreateCourtSubmission input, CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await submissions.SubmitAsync(input, cancellationToken);
            // No public read endpoint exists for pending community submissions.
            return StatusCode(StatusCodes.Status201Created, receipt);
        }
        catch (ArgumentException error)
        {
            ModelState.AddModelError(error.ParamName ?? "submission", error.Message.Split(" (Parameter")[0]);
            return ValidationProblem(ModelState);
        }
        catch (DbUpdateException)
        {
            logger.LogError("Failed to persist a court submission.");
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "We could not save your submission. Please try again.");
        }
    }
}
