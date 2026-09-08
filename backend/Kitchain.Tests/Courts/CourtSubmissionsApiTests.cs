using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitchain.Application.Courts;
using Kitchain.Domain.Courts;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Tests.Courts;

public sealed class CourtSubmissionsApiTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [PostgresFact]
    public async Task Post_persists_pending_submission_children_without_creating_a_live_court()
    {
        await using var db = fixture.CreateDbContext();
        var courtCount = await db.Courts.CountAsync();
        using var response = await fixture.Client.PostAsJsonAsync("/api/court-submissions", new CreateCourtSubmission
        {
            Name = "Community venue", City = "Makati", Address = "Test address", NumberOfCourts = 2,
            IndoorOutdoor = IndoorOutdoorType.Indoor, BookingMethod = BookingMethod.Phone, Phone = "09170000000",
            Amenities = [AmenityCode.Parking, AmenityCode.Shower],
            Photos = [new("https://example.com/court.jpg", "Playing area")]
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<CourtSubmissionReceipt>(Json);
        Assert.NotNull(receipt);
        var saved = await db.Set<CourtSubmission>().Include(s => s.Amenities).Include(s => s.Photos)
            .SingleAsync(s => s.Id == receipt.Id);
        Assert.Equal(CourtSubmissionStatus.Pending, saved.Status);
        Assert.Equal(saved.Status, receipt.Status);
        // PostgreSQL stores microseconds; DateTimeOffset can contain finer ticks.
        Assert.InRange(Math.Abs((saved.SubmittedAt - receipt.SubmittedAt).TotalMilliseconds), 0, 0.001);
        Assert.Equal(2, saved.Amenities.Count);
        Assert.True(Assert.Single(saved.Photos).IsPrimary);
        Assert.Equal(courtCount, await db.Courts.CountAsync());
        using var detail = await fixture.Client.GetAsync($"/api/courts/{receipt.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [PostgresTheory]
    [InlineData("{}")]
    [InlineData("{\"name\":\"Venue\",\"city\":\"Makati\",\"address\":\"Street\",\"numberOfCourts\":2,\"indoorOutdoor\":\"Mixed\",\"bookingMethod\":\"WalkIn\",\"status\":\"Approved\"}")]
    public async Task Missing_required_fields_and_client_status_are_rejected(string json)
    {
        await using var db = fixture.CreateDbContext();
        var count = await db.Set<CourtSubmission>().CountAsync();
        using var response = await fixture.Client.PostAsync("/api/court-submissions", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(count, await db.Set<CourtSubmission>().CountAsync());
    }
}
