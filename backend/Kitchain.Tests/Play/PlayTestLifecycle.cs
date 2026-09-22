using Kitchain.Domain.Play;
using Kitchain.Application.Play;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kitchain.Tests.Play;

internal static class PlayTestLifecycle
{
    internal static async Task<PlaySessionDetail> StartReadyGames(this HttpClient client, string url)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var state = (await client.GetFromJsonAsync<PlaySessionDetail>(url, json))!;
        foreach (var match in state.CurrentMatches.Where(m => m.Status == PlayMatchStatus.Ready))
        {
            using var result = await client.PostAsJsonAsync($"{url}/matches/{match.Id}/start", new { expectedRevision = match.LineupRevision });
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
        }
        return (await client.GetFromJsonAsync<PlaySessionDetail>(url, json))!;
    }
    // Legacy scoring/rotation scenarios explicitly accept Ready lineups before exercising gameplay.
    internal static void StartReadyGames(this PlaySession session)
    {
        foreach (var match in session.Matches.Where(m => m.IsCurrent && m.Status == PlayMatchStatus.Ready).OrderBy(m => m.CourtNumber))
            session.StartGame(match.Id, match.LineupRevision, session.UpdatedAt);
    }
}
