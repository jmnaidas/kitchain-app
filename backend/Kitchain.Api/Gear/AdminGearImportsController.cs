using Kitchain.Application.Gear;
using Kitchain.Domain.Gear;
using Microsoft.AspNetCore.Mvc;

namespace Kitchain.Api.Gear;

// Unauthenticated portfolio preview, consistent with Courts moderation. Not a secured admin boundary.
[ApiController]
[Route("api/admin/gear/imports")]
[RequestSizeLimit(32768)]
public sealed class AdminGearImportsController(GearImportService service) : GearControllerBase
{
    [HttpGet]
    public Task<ActionResult<IReadOnlyList<GearImportSummary>>> List(CancellationToken ct,
        [FromQuery] GearImportStatus status = GearImportStatus.Pending, [FromQuery] int offset = 0) =>
        Execute(() => service.ListAsync(status, offset, ct));
    [HttpGet("{id:guid}")]
    public Task<ActionResult<GearImportDetail?>> Find(Guid id, CancellationToken ct) => Execute(() => service.FindAsync(id, ct));
    [HttpPost]
    public Task<ActionResult<GearImportDetail>> Create(CreateGearImport input, CancellationToken ct) =>
        Execute(() => service.CreateAsync(input, ct), result => CreatedAtAction(nameof(Find), new { id = result.Id }, result));
    [HttpPost("{id:guid}/approve")]
    public Task<ActionResult<GearImportDetail>> Approve(Guid id, ApproveGearImport input, CancellationToken ct) =>
        Execute(() => service.ApproveAsync(id, input, ct));
    [HttpPost("{id:guid}/reject")]
    public Task<ActionResult<GearImportDetail>> Reject(Guid id, RejectGearImport input, CancellationToken ct) =>
        Execute(() => service.RejectAsync(id, input, ct));
}
