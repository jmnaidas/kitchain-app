using Kitchain.Application.Gear;
using Microsoft.AspNetCore.Mvc;

namespace Kitchain.Api.Gear;

// Unauthenticated portfolio preview; source registration performs no network requests.
[ApiController]
[Route("api/admin/gear/sources")]
[RequestSizeLimit(8192)]
public sealed class AdminGearSourcesController(GearImportService service) : GearControllerBase
{
    [HttpGet]
    public Task<ActionResult<IReadOnlyList<GearSourceDetail>>> List(CancellationToken ct, [FromQuery] int offset = 0) =>
        Execute(() => service.SourcesAsync(offset, ct));
    [HttpPost]
    public Task<ActionResult<GearSourceDetail>> Create(CreateGearSource input, CancellationToken ct) =>
        Execute(() => service.CreateSourceAsync(input, ct), result => StatusCode(201, result));
}
