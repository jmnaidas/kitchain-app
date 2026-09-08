using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitchain.Application.Courts;
using Kitchain.Domain.Courts;
using Kitchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Tests.Courts;

public sealed class CourtsApiTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [PostgresFact]
    public async Task List_returns_only_published_venues_with_readable_metadata()
    {
        using var response = await fixture.Client.GetAsync("/api/courts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<CourtPage>(Json);
        Assert.NotNull(page);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(20, page.PageSize);
        Assert.All(page.Items, court =>
        {
            Assert.Contains("(fictional)", court.Name);
            Assert.Equal(AvailabilityIntegrationStatus.NotIntegrated, court.Availability.Status);
        });
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"indoorOutdoor\":\"Indoor\"", raw);
        Assert.Contains("\"status\":\"NotIntegrated\"", raw);
        Assert.DoesNotContain("Pending Manila", raw);
        Assert.DoesNotContain("Retired Taguig", raw);
    }

    [PostgresTheory]
    [InlineData("city=%20mAkAtI%20", 2)]
    [InlineData("city=Quezon%20City", 1)]
    [InlineData("city=NoSuchCity", 0)]
    [InlineData("city=%25", 0)]
    [InlineData("indoorOutdoor=Indoor", 2)]
    [InlineData("indoorOutdoor=Outdoor", 2)]
    [InlineData("indoorOutdoor=Mixed", 1)]
    [InlineData("amenity=Parking", 2)]
    [InlineData("minCourts=5", 2)]
    [InlineData("maxStartingPrice=450", 2)]
    [InlineData("maxStartingPrice=300", 1)]
    [InlineData("maxStartingPrice=9999&currencyCode=USD", 0)]
    [InlineData("city=Makati&indoorOutdoor=Indoor&amenity=Parking&minCourts=6&maxStartingPrice=600", 1)]
    public async Task Filters_execute_in_postgresql(string query, int count)
    {
        var page = await fixture.Client.GetFromJsonAsync<CourtPage>($"/api/courts?{query}", Json);
        Assert.NotNull(page);
        Assert.Equal(count, page.TotalCount);
        Assert.Equal(count, page.Items.Count);
    }

    [PostgresFact]
    public async Task Pagination_is_stable_and_preserves_total_count()
    {
        var first = await fixture.Client.GetFromJsonAsync<CourtPage>("/api/courts?pageSize=2", Json);
        var second = await fixture.Client.GetFromJsonAsync<CourtPage>("/api/courts?page=2&pageSize=2", Json);
        var beyond = await fixture.Client.GetFromJsonAsync<CourtPage>("/api/courts?page=9&pageSize=2", Json);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(beyond);
        Assert.Equal(5, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(2, second.Page);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(2, second.Items.Count);
        Assert.Empty(first.Items.Select(c => c.Id).Intersect(second.Items.Select(c => c.Id)));
        Assert.Empty(beyond.Items);
        Assert.Equal(5, beyond.TotalCount);
    }

    [PostgresFact]
    public async Task Detail_returns_a_richer_published_venue()
    {
        var detail = await fixture.Client.GetFromJsonAsync<CourtDetail>("/api/courts/00000000-0000-4000-8000-000000000001", Json);
        Assert.NotNull(detail);
        Assert.Equal("Makati", detail.City);
        Assert.Equal("Metro Manila", detail.Region);
        Assert.Equal(600m, detail.StartingPrice);
        Assert.Equal("PHP", detail.CurrencyCode);
        Assert.Equal(PriceUnit.PerHour, detail.PriceUnit);
        Assert.Equal(4, detail.Amenities.Count);
        Assert.Equal("https://example.com/fictional-booking/1", detail.BookingUrl);
        Assert.Contains("Sample only", detail.OpeningHours);
    }

    [PostgresTheory]
    [InlineData("00000000-0000-4000-8000-000000000099")]
    [InlineData("00000000-0000-4000-8000-000000000006")]
    [InlineData("00000000-0000-4000-8000-000000000007")]
    [InlineData("not-a-guid")]
    public async Task Missing_or_nonpublic_detail_returns_404(string id)
    {
        using var response = await fixture.Client.GetAsync($"/api/courts/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [PostgresTheory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647")]
    [InlineData("page=abc")]
    [InlineData("minCourts=0")]
    [InlineData("maxStartingPrice=-1")]
    [InlineData("currencyCode=P1P")]
    [InlineData("indoorOutdoor=Unknown")]
    [InlineData("indoorOutdoor=99")]
    [InlineData("amenity=Unknown")]
    public async Task Invalid_query_returns_validation_problem(string query)
    {
        using var response = await fixture.Client.GetAsync($"/api/courts?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("stackTrace", await response.Content.ReadAsStringAsync());
    }

    [PostgresFact]
    public async Task Migration_seeds_lookup_and_development_seed_is_idempotent()
    {
        await using var db = fixture.CreateDbContext();
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(13, await db.Set<Amenity>().CountAsync());
        Assert.Equal(7, await db.Courts.CountAsync());
        Assert.Equal(0, await DevelopmentCourtSeeder.SeedAsync(db));
        Assert.Equal(7, await db.Courts.CountAsync());
    }

    [PostgresFact]
    public async Task OpenApi_describes_read_routes_and_string_enums()
    {
        var json = await fixture.Client.GetStringAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/courts").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/courts/{id}").TryGetProperty("get", out _));
        Assert.False(paths.GetProperty("/api/courts").TryGetProperty("post", out _));
        var operation = paths.GetProperty("/api/courts").GetProperty("get");
        Assert.True(operation.GetProperty("responses").GetProperty("400").GetProperty("content")
            .TryGetProperty("application/problem+json", out _));
        Assert.Contains(operation.GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "pageSize" &&
                parameter.GetProperty("schema").GetProperty("default").GetInt32() == 20);
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("IndoorOutdoorType");
        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Contains(schema.GetProperty("enum").EnumerateArray(), value => value.GetString() == "Mixed");
    }
}
