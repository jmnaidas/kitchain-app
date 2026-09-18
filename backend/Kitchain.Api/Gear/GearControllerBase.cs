using System.Data.Common;
using Kitchain.Domain.Gear;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Api.Gear;

public abstract class GearControllerBase : ControllerBase
{
    protected async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action, Func<T, ActionResult<T>>? success = null)
    {
        try
        {
            var result = await action();
            return result is null ? NotFound() : success is null ? Ok(result) : success(result);
        }
        catch (ArgumentException error)
        {
            ModelState.AddModelError(error.ParamName ?? "request", error.Message);
            return ValidationProblem(ModelState);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (GearConflictException error) { return Problem(statusCode: 409, title: error.Message); }
        catch (Exception error) when (error is DbUpdateException or DbException)
        {
            // Do not include connection strings or database exception details in responses/logs.
            HttpContext.RequestServices.GetRequiredService<ILogger<GearControllerBase>>()
                .LogError("Gear persistence operation failed for {Path}.", Request.Path);
            return Problem(statusCode: 503, title: "Gear data is temporarily unavailable. Reload before retrying a decision.");
        }
    }
}
