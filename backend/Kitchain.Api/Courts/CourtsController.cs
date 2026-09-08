using Kitchain.Application.Courts;
using Microsoft.AspNetCore.Mvc;

namespace Kitchain.Api.Courts;

[ApiController]
[Route("api/courts")]
public sealed class CourtsController(CourtDiscoveryService discovery) : ControllerBase
{
    /// <summary>Discover published venues. Filters combine with AND; Mixed is its own classification.</summary>
    [HttpGet]
    [ProducesResponseType<CourtPage>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CourtPage>> Search([FromQuery] CourtSearch search, CancellationToken cancellationToken) =>
        Ok(await discovery.SearchAsync(search, cancellationToken));

    /// <summary>Read one published venue; missing and non-public venues are indistinguishable.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CourtDetail>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CourtDetail>> Find(Guid id, CancellationToken cancellationToken)
    {
        var court = await discovery.FindAsync(id, cancellationToken);
        return court is null ? NotFound() : Ok(court);
    }
}
