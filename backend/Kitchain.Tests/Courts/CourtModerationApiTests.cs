using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitchain.Application.Courts;
using Kitchain.Domain.Courts;
using Kitchain.Infrastructure.Courts;
using Kitchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kitchain.Tests.Courts;

public sealed class CourtModerationApiTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private const string Route = "/api/admin/court-submissions";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [PostgresFact]
    public async Task Pending_queue_filters_status_and_reads_complete_details_with_missing_and_invalid_handling()
    {
        var pending = await CreateAsync();
        var rejected = await CreateAsync();
        using var reject = await fixture.Client.PostAsync($"{Route}/{rejected.Id}/reject", null);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var items = await fixture.Client.GetFromJsonAsync<CourtSubmissionSummary[]>($"{Route}?status=Pending", Json);
        Assert.NotNull(items);
        Assert.Contains(items, item => item.Id == pending.Id);
        Assert.DoesNotContain(items, item => item.Id == rejected.Id);
        Assert.All(items, item => Assert.Equal(CourtSubmissionStatus.Pending, item.Status));
        Assert.Equal(items.OrderBy(item => item.SubmittedAt).ThenBy(item => item.Id).Select(item => item.Id), items.Select(item => item.Id));
        var detail = await fixture.Client.GetFromJsonAsync<CourtSubmissionReview>($"{Route}/{pending.Id}", Json);
        Assert.NotNull(detail);
        Assert.Equal(pending.Address, detail.Address);
        Assert.Equal(pending.OpeningHours, detail.OpeningHours);
        Assert.Equal(2, detail.Amenities.Count);
        Assert.Equal(2, detail.Photos.Count);
        Assert.True(detail.Photos[0].IsPrimary);
        using var invalid = await fixture.Client.GetAsync($"{Route}?status=999");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var missingId = Guid.NewGuid();
        using var missing = await fixture.Client.GetAsync($"{Route}/{missingId}");
        using var missingApproval = await fixture.Client.PostAsync($"{Route}/{missingId}/approve", null);
        using var missingRejection = await fixture.Client.PostAsync($"{Route}/{missingId}/reject", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingApproval.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingRejection.StatusCode);
    }

    [PostgresFact]
    public async Task Approval_publishes_community_venue_and_copies_all_metadata_without_changing_the_submission_data()
    {
        var submission = await CreateAsync();
        using var response = await fixture.Client.PostAsync($"{Route}/{submission.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<CourtModerationReceipt>(Json);
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.CourtId);
        Assert.Equal(submission.Id, receipt.SubmissionId);
        Assert.Equal(CourtSubmissionStatus.Approved, receipt.SubmissionStatus);
        var detail = await fixture.Client.GetFromJsonAsync<CourtDetail>($"/api/courts/{receipt.CourtId}", Json);
        Assert.NotNull(detail);
        Assert.Equal(CourtDataSource.CommunitySupplied, detail.DataSource);
        Assert.Equal(submission.Name, detail.Name);
        Assert.Equal(submission.Address, detail.Address);
        Assert.Equal(submission.City, detail.City);
        Assert.Equal(submission.Region, detail.Region);
        Assert.Equal(submission.Latitude, detail.Latitude);
        Assert.Equal(submission.Longitude, detail.Longitude);
        Assert.Equal(submission.NumberOfCourts, detail.NumberOfCourts);
        Assert.Equal(submission.IndoorOutdoor, detail.IndoorOutdoor);
        Assert.Equal(submission.Surface, detail.Surface);
        Assert.Equal(submission.OpeningHours, detail.OpeningHours);
        Assert.Equal(submission.StartingPrice, detail.StartingPrice);
        Assert.Equal(submission.CurrencyCode, detail.CurrencyCode);
        Assert.Equal(submission.PriceUnit, detail.PriceUnit);
        Assert.Equal(submission.Phone, detail.Phone);
        Assert.Equal(submission.WebsiteUrl, detail.WebsiteUrl);
        Assert.Equal(submission.SocialUrl, detail.SocialUrl);
        Assert.Equal(submission.BookingUrl, detail.BookingUrl);
        Assert.Equal(submission.BookingMethod, detail.BookingMethod);
        Assert.Equal(submission.Amenities.Select(a => a.AmenityCode).Order(), detail.Amenities.Order());
        var expectedPhotos = submission.Photos.OrderByDescending(p => p.IsPrimary).ThenBy(p => p.DisplayOrder);
        Assert.Equal(expectedPhotos.Select(p => (p.ImageUrl, p.AltText, p.DisplayOrder, p.IsPrimary)),
            detail.Photos.Select(p => (p.ImageUrl, p.AltText, p.DisplayOrder, p.IsPrimary)));
        Assert.Empty(submission.Photos.Select(p => p.Id).Intersect(detail.Photos.Select(p => p.Id)));
        var page = await fixture.Client.GetFromJsonAsync<CourtPage>($"/api/courts?city={submission.City}", Json);
        Assert.NotNull(page);
        Assert.Contains(page.Items, court => court.Id == receipt.CourtId);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(CourtStatus.Published, (await db.Courts.SingleAsync(c => c.Id == receipt.CourtId)).Status);
        var saved = await db.Set<CourtSubmission>().Include(s => s.Photos).SingleAsync(s => s.Id == submission.Id);
        Assert.Equal(CourtSubmissionStatus.Approved, saved.Status);
        Assert.True(saved.UpdatedAt > saved.SubmittedAt);
        Assert.Equal(2, saved.Photos.Count);
        using var repeated = await fixture.Client.PostAsync($"{Route}/{submission.Id}/approve", null);
        using var reject = await fixture.Client.PostAsync($"{Route}/{submission.Id}/reject", null);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reject.StatusCode);
    }

    [PostgresFact]
    public async Task Rejection_keeps_history_without_publishing_and_disallows_further_decisions()
    {
        var submission = await CreateAsync();
        using var response = await fixture.Client.PostAsync($"{Route}/{submission.Id}/reject", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<CourtModerationReceipt>(Json);
        Assert.NotNull(receipt);
        Assert.Null(receipt.CourtId);
        Assert.Equal(CourtSubmissionStatus.Rejected, receipt.SubmissionStatus);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Courts.AnyAsync(c => c.Name == submission.Name));
        var saved = await db.Set<CourtSubmission>().SingleAsync(s => s.Id == submission.Id);
        Assert.Equal(CourtSubmissionStatus.Rejected, saved.Status);
        Assert.True(saved.UpdatedAt > saved.SubmittedAt);
        using var repeated = await fixture.Client.PostAsync($"{Route}/{submission.Id}/reject", null);
        using var approve = await fixture.Client.PostAsync($"{Route}/{submission.Id}/approve", null);
        using var publicDetail = await fixture.Client.GetAsync($"/api/courts/{submission.Id}");
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, publicDetail.StatusCode);
    }

    [PostgresFact]
    public async Task Concurrent_approvals_publish_exactly_once()
    {
        var submission = await CreateAsync();
        var responses = await Task.WhenAll(
            fixture.Client.PostAsync($"{Route}/{submission.Id}/approve", null),
            fixture.Client.PostAsync($"{Route}/{submission.Id}/approve", null));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await using var db = fixture.CreateDbContext();
            Assert.Equal(1, await db.Courts.CountAsync(c => c.Name == submission.Name));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [PostgresFact]
    public async Task Failure_after_saving_sql_rolls_back_court_children_and_submission_status()
    {
        var submission = await CreateAsync();
        var options = new DbContextOptionsBuilder<KitchainDbContext>().UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(new FailAfterSave()).Options;
        await using var db = new KitchainDbContext(options);
        await Assert.ThrowsAsync<DbUpdateException>(() => new EfCourtSubmissionModeration(db)
            .DecideAsync(submission.Id, CourtSubmissionDecision.Approve, default));
        var attemptedCourtId = Assert.Single(db.ChangeTracker.Entries<Court>()).Entity.Id;
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.Courts.AnyAsync(c => c.Id == attemptedCourtId));
        Assert.False(await verify.Set<CourtPhoto>().AnyAsync(p => p.CourtId == attemptedCourtId));
        Assert.False(await verify.Set<CourtAmenity>().AnyAsync(a => a.CourtId == attemptedCourtId));
        var saved = await verify.Set<CourtSubmission>().SingleAsync(s => s.Id == submission.Id);
        Assert.Equal(CourtSubmissionStatus.Pending, saved.Status);
        Assert.Equal(saved.SubmittedAt, saved.UpdatedAt);
    }

    private async Task<CourtSubmission> CreateAsync()
    {
        var now = DateTimeOffset.UtcNow.AddDays(-1);
        var submission = new CourtSubmission(Guid.NewGuid(), $"Review venue {Guid.NewGuid():N}", "Review address",
            $"City-{Guid.NewGuid():N}", 3, IndoorOutdoorType.Mixed, BookingMethod.Website, now,
            [AmenityCode.Parking, AmenityCode.Shower], "Metro Manila", 14.55m, 121.02m,
            "Acrylic", "Daily 8am–8pm", 300m, "PHP", PriceUnit.PerHour, "09170000000",
            "https://example.com/venue", "https://example.com/social", "https://example.com/book");
        submission.AddPhoto(new CourtSubmissionPhoto(Guid.NewGuid(), submission.Id, "https://example.com/side.jpg", 0, false, now, "Side view"));
        submission.AddPhoto(new CourtSubmissionPhoto(Guid.NewGuid(), submission.Id, "https://example.com/main.jpg", 2, true, now, "Main court"));
        await using var db = fixture.CreateDbContext();
        db.Set<CourtSubmission>().Add(submission);
        await db.SaveChangesAsync();
        return submission;
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => throw new DbUpdateException("Injected failure before transaction commit.");
    }
}
