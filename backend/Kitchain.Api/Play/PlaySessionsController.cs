using System.Data.Common;
using Kitchain.Application.Play;
using Kitchain.Domain.Play;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Api.Play;

// Public foundation only; organizer/player authorization is not implemented in Phase 1A.
[ApiController]
[Route("api/play/sessions")]
public sealed class PlaySessionsController(PlaySessionService sessions, ILogger<PlaySessionsController> logger,
    IPlaySessionNotifier notifier) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(8192)]
    [ProducesResponseType<PlaySessionDetail>(StatusCodes.Status201Created)]
    public Task<ActionResult<PlaySessionDetail>> Create(CreatePlaySession input, CancellationToken cancellationToken) =>
        Execute<PlaySessionDetail>(async () =>
        {
            var session = await sessions.CreateAsync(input, cancellationToken);
            return CreatedAtAction(nameof(Find), new { code = session.JoinCode }, session);
        });

    [HttpPatch("{code}")]
    [RequestSizeLimit(8192)]
    public Task<ActionResult<PlaySessionDetail>> Edit(string code, CreatePlaySession input, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.EditAsync(code, input, cancellationToken));

    [HttpGet("{code}")]
    [ProducesResponseType<PlaySessionDetail>(StatusCodes.Status200OK)]
    public Task<ActionResult<PlaySessionDetail>> Find(string code, CancellationToken cancellationToken) =>
        Execute<PlaySessionDetail>(async () =>
        {
            var session = await sessions.FindAsync(code, cancellationToken);
            return session is null ? NotFound() : Ok(session);
        });

    [HttpPost("{code}/players")]
    [RequestSizeLimit(2048)]
    [ProducesResponseType<PlayPlayerDetail>(StatusCodes.Status201Created)]
    public Task<ActionResult<PlayPlayerDetail>> AddGuest(string code, AddPlayGuest input, CancellationToken cancellationToken) =>
        Execute<PlayPlayerDetail>(async () =>
        {
            var player = await sessions.AddGuestAsync(code, input, cancellationToken);
            if (player is not null) await notifier.ChangedAsync(PlaySession.NormalizeCode(code));
            return player is null ? NotFound() : StatusCode(StatusCodes.Status201Created, player);
        });

    [HttpPost("{code}/players/{playerId:guid}/rest")]
    public Task<ActionResult<PlaySessionDetail>> Rest(string code, Guid playerId, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.RestAsync(code, playerId, cancellationToken));

    [HttpPost("{code}/players/{playerId:guid}/rejoin")]
    public Task<ActionResult<PlaySessionDetail>> Rejoin(string code, Guid playerId, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.RejoinAsync(code, playerId, cancellationToken));

    [HttpPost("{code}/start")]
    public Task<ActionResult<PlaySessionDetail>> Start(string code, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.StartAsync(code, cancellationToken));

    [HttpPost("{code}/matches/{matchId:guid}/finish")]
    public Task<ActionResult<PlaySessionDetail>> FinishGame(string code, Guid matchId, CancellationToken cancellationToken,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] FinishPlayGame? input = null) =>
        SessionResult(() => sessions.FinishGameAsync(code, matchId, cancellationToken, input?.Winner));

    [HttpPatch("{code}/matches/{matchId:guid}/rallies/{rallyId:guid}/call-out")]
    [RequestSizeLimit(2048)]
    public Task<ActionResult<PlaySessionDetail>> EditRallyCallOut(string code, Guid matchId, Guid rallyId,
        EditPlayRallyCallOut input, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.EditRallyCallOutAsync(code, matchId, rallyId, input, cancellationToken));

    [HttpPost("{code}/matches/{matchId:guid}/rallies")]
    [RequestSizeLimit(2048)]
    public Task<ActionResult<PlaySessionDetail>> RecordRally(string code, Guid matchId, RecordPlayRally input, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.RecordRallyAsync(code, matchId, input, cancellationToken));

    [HttpPatch("{code}/matches/{matchId:guid}/score")]
    [RequestSizeLimit(2048)]
    public Task<ActionResult<PlaySessionDetail>> CorrectScore(string code, Guid matchId, CorrectPlayScore input, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.CorrectScoreAsync(code, matchId, input, cancellationToken));

    [HttpPatch("{code}/players/{playerId:guid}")]
    [RequestSizeLimit(2048)]
    public Task<ActionResult<PlaySessionDetail>> RenameGuest(string code, Guid playerId, AddPlayGuest input, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.RenameGuestAsync(code, playerId, input, cancellationToken));

    [HttpDelete("{code}/players/{playerId:guid}")]
    public Task<ActionResult<PlaySessionDetail>> RemoveGuest(string code, Guid playerId, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.RemoveGuestAsync(code, playerId, cancellationToken));

    [HttpPost("{code}/end")]
    public Task<ActionResult<PlaySessionDetail>> End(string code, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.EndAsync(code, cancellationToken));

    [HttpPost("{code}/matches/{matchId:guid}/next")]
    [RequestSizeLimit(2048)]
    public Task<ActionResult<PlaySessionDetail>> StartNextGame(string code, Guid matchId, StartNextPlayGame input, CancellationToken cancellationToken) =>
        SessionResult(() => sessions.StartNextGameAsync(code, matchId, input, cancellationToken));

    [HttpPatch("{code}/matches/{matchId:guid}/lineup")]
    [RequestSizeLimit(2048)]
    public Task<ActionResult<PlaySessionDetail>> ChangeLineup(string code, Guid matchId, ChangePlayLineupSlot input, CancellationToken ct) =>
        SessionResult(() => sessions.ChangeLineupSlotAsync(code, matchId, input, ct));

    [HttpPost("{code}/matches/{matchId:guid}/lineup/reset")]
    public Task<ActionResult<PlaySessionDetail>> ResetLineup(string code, Guid matchId, ReadyPlayGame input, CancellationToken ct) =>
        SessionResult(() => sessions.ResetRecommendationAsync(code, matchId, input, ct));

    [HttpPost("{code}/matches/{matchId:guid}/start")]
    public Task<ActionResult<PlaySessionDetail>> StartGame(string code, Guid matchId, ReadyPlayGame input, CancellationToken ct) =>
        SessionResult(() => sessions.StartGameAsync(code, matchId, input, ct));

    private Task<ActionResult<PlaySessionDetail>> SessionResult(Func<Task<PlaySessionDetail?>> operation) =>
        Execute<PlaySessionDetail>(async () =>
        {
            var session = await operation();
            if (session is not null) await notifier.ChangedAsync(session.JoinCode);
            return session is null ? NotFound() : Ok(session);
        });

    private async Task<ActionResult<T>> Execute<T>(Func<Task<ActionResult<T>>> operation)
    {
        try { return await operation(); }
        catch (ArgumentException error)
        {
            ModelState.AddModelError(error.ParamName ?? "session", error.Message.Split(" (Parameter")[0]);
            return ValidationProblem(ModelState);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PlayConflictException error) { return Problem(statusCode: StatusCodes.Status409Conflict, title: error.Message); }
        catch (Exception error) when (error is DbUpdateException or DbException)
        {
            logger.LogError("Failed to read or persist a Play session operation.");
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "We could not confirm this operation. Reload the session before trying again.");
        }
    }
}
