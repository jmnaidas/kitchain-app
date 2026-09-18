using Kitchain.Application.Gear;
using Microsoft.AspNetCore.Mvc;

namespace Kitchain.Api.Gear;

[ApiController]
[Route("api/gear/paddles")]
public sealed class GearPaddlesController(IGearCatalogReader reader) : GearControllerBase
{
    [HttpGet]
    public Task<ActionResult<IReadOnlyList<GearPaddleSummary>>> List(CancellationToken ct, [FromQuery] int offset = 0) =>
        Execute(() => reader.ListAsync(offset, ct));
    [HttpGet("{id:guid}")]
    public Task<ActionResult<GearPaddleDetail?>> Find(Guid id, CancellationToken ct) => Execute(() => reader.FindAsync(id, ct));
}
