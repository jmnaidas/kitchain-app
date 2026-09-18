using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitchain.Application.Gear;
using Kitchain.Domain.Gear;
using Kitchain.Tests.Courts;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Tests.Gear;

public sealed class GearApiTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    private HttpClient Client => fixture.Client;
    private async Task<T> Body<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
    private async Task<GearSourceDetail> Source() => await Body<GearSourceDetail>(await Client.PostAsJsonAsync("/api/admin/gear/sources",
        new CreateGearSource { Type = GearSourceType.Manual, Name = "Synthetic test source", RetrievedAt = DateTimeOffset.UtcNow.AddDays(-1) }, Json), HttpStatusCode.Created);
    private async Task<GearImportDetail> Candidate(Guid sourceId, string? suffix = null, string variant = "16-mm") =>
        await Body<GearImportDetail>(await Client.PostAsJsonAsync("/api/admin/gear/imports",
            GearImportServiceTests.Proposal(sourceId, suffix ?? Guid.NewGuid().ToString("N")) with { VariantSlug = variant }, Json), HttpStatusCode.Created);
    private Task<HttpResponseMessage> Approve(Guid id, Guid? target = null) => Client.PostAsJsonAsync($"/api/admin/gear/imports/{id}/approve", new ApproveGearImport(target), Json);

    [PostgresFact]
    public async Task Review_roundtrip_publishes_only_after_approval_and_preserves_conflicting_sources()
    {
        var source = await Source(); var suffix = Guid.NewGuid().ToString("N");
        var candidate = await Candidate(source.Id, suffix);
        var pending = await Body<GearImportSummary[]>(await Client.GetAsync("/api/admin/gear/imports"));
        Assert.Contains(pending, c => c.Id == candidate.Id);
        var stored = await Body<GearImportDetail>(await Client.GetAsync($"/api/admin/gear/imports/{candidate.Id}"));
        // PostgreSQL timestamps retain microseconds, while .NET timestamps have 100ns ticks.
        Assert.InRange((candidate.CreatedAt - stored.CreatedAt).Ticks, 0, 9);
        Assert.InRange((candidate.ObservedAt - stored.ObservedAt).Ticks, 0, 9);
        Assert.Equal(candidate with { CreatedAt = stored.CreatedAt, ObservedAt = stored.ObservedAt }, stored);
        var before = await Body<GearPaddleSummary[]>(await Client.GetAsync("/api/gear/paddles"));
        Assert.DoesNotContain(before, p => p.Brand.Name == candidate.BrandName);
        var approved = await Body<GearImportDetail>(await Approve(candidate.Id));
        Assert.Equal(GearImportStatus.Approved, approved.Status);
        Assert.NotNull(approved.ReviewedAt);
        var publicList = await Body<GearPaddleSummary[]>(await Client.GetAsync("/api/gear/paddles"));
        var paddle = Assert.Single(publicList, p => p.Brand.Name == candidate.BrandName);
        Assert.Equal(approved.MatchedVariantId, Assert.Single(paddle.Variants).Id);
        var secondSource = await Source();
        var conflicting = await Body<GearImportDetail>(await Client.PostAsJsonAsync("/api/admin/gear/imports",
            GearImportServiceTests.Proposal(secondSource.Id, suffix) with
            {
                EvidenceType = GearEvidenceType.IndependentlyMeasured,
                Specifications = new() { ThicknessMm = 15.9m, MeasuredWeightOz = 8.27m }
            }, Json), HttpStatusCode.Created);
        await Body<GearImportDetail>(await Approve(conflicting.Id, approved.MatchedVariantId));
        var detail = await Body<GearPaddleDetail>(await Client.GetAsync($"/api/gear/paddles/{paddle.Id}"));
        var variant = Assert.Single(detail.Variants);
        Assert.Equal(2, variant.Evidence.Count);
        Assert.Contains(variant.Evidence, e => e.Source.Id == source.Id && e.Specifications.ThicknessMm == 16);
        Assert.Contains(variant.Evidence, e => e.Source.Id == secondSource.Id && e.Specifications.ThicknessMm == 15.9m);
        Assert.Null(variant.KitchainPerformance);
        Assert.Equal(HttpStatusCode.Conflict, (await Approve(candidate.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"/api/admin/gear/imports/{candidate.Id}/reject", new { })).StatusCode);
        Assert.DoesNotContain(await Body<GearImportSummary[]>(await Client.GetAsync("/api/admin/gear/imports")), c => c.Id == candidate.Id);
    }

    [PostgresFact]
    public async Task Duplicate_variant_rolls_back_whole_decision_and_multiple_variants_share_product()
    {
        var source = await Source(); var suffix = Guid.NewGuid().ToString("N");
        var first = await Candidate(source.Id, suffix);
        var approved = await Body<GearImportDetail>(await Approve(first.Id));
        var duplicate = await Candidate(source.Id, suffix);
        Assert.Equal(HttpStatusCode.Conflict, (await Approve(duplicate.Id)).StatusCode);
        var stillPending = await Body<GearImportDetail>(await Client.GetAsync($"/api/admin/gear/imports/{duplicate.Id}"));
        Assert.Equal(GearImportStatus.Pending, stillPending.Status); Assert.Null(stillPending.ReviewedAt);
        var other = await Candidate(source.Id, suffix, "14-mm");
        await Body<GearImportDetail>(await Approve(other.Id));
        await using var db = fixture.CreateDbContext();
        var firstVariant = await db.Set<PaddleVariant>().SingleAsync(v => v.Id == approved.MatchedVariantId);
        Assert.Equal(2, await db.Set<PaddleVariant>().CountAsync(v => v.PaddleId == firstVariant.PaddleId));
        Assert.Single(await db.Set<PaddleSpecificationEvidence>().Where(e => e.PaddleVariantId == firstVariant.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task Concurrent_review_has_one_winner_and_rejected_candidate_cannot_publish()
    {
        var candidate = await Candidate((await Source()).Id);
        var results = await Task.WhenAll(Approve(candidate.Id), Approve(candidate.Id));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var rejected = await Candidate(candidate.SourceId);
        await Body<GearImportDetail>(await Client.PostAsJsonAsync($"/api/admin/gear/imports/{rejected.Id}/reject", new { notes = "Synthetic rejection" }));
        Assert.Equal(HttpStatusCode.Conflict, (await Approve(rejected.Id)).StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Set<GearBrand>().AnyAsync(b => b.Slug == rejected.BrandSlug));
    }

    [PostgresFact]
    public async Task Public_reads_hide_drafts_archives_and_unpublished_profiles_but_keep_commercial_provenance()
    {
        var source = await Source(); var now = DateTimeOffset.UtcNow;
        await using var db = fixture.CreateDbContext();
        var brand = new GearBrand(Guid.NewGuid(), "Synthetic visibility " + Guid.NewGuid(), "visibility-" + Guid.NewGuid().ToString("N"), now);
        var draft = new Paddle(Guid.NewGuid(), brand.Id, "Draft", "draft", now);
        var live = new Paddle(Guid.NewGuid(), brand.Id, "Published", "published", now);
        var archived = new Paddle(Guid.NewGuid(), brand.Id, "Archived", "archived", now);
        live.SetStatus(GearPublicationStatus.Published, now); archived.SetStatus(GearPublicationStatus.Archived, now);
        var variant = new PaddleVariant(Guid.NewGuid(), live.Id, "Synthetic 16", "16", now);
        var hidden = new PaddleVariant(Guid.NewGuid(), live.Id, "Synthetic hidden", "hidden", now);
        variant.SetStatus(GearPublicationStatus.Published, now);
        var profile = new PaddlePerformanceProfile(variant.Id, "synthetic-test-v1", now, control: 8);
        var image = new PaddleImage(Guid.NewGuid(), variant.Id, "https://example.test/synthetic.png", 1, true, now, source.Id);
        var listing = new PaddleListing(Guid.NewGuid(), variant.Id, source.Id, "Fictional seller", "https://example.test/synthetic", "PHP", 10, null, null, now, now);
        db.AddRange(brand, draft, live, archived, variant, hidden, profile, image, listing); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/gear/paddles/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/gear/paddles/{archived.Id}")).StatusCode);
        var detail = await Body<GearPaddleDetail>(await Client.GetAsync($"/api/gear/paddles/{live.Id}"));
        var visible = Assert.Single(detail.Variants);
        Assert.Equal(variant.Id, visible.Id); Assert.Null(visible.KitchainPerformance);
        Assert.Equal(source.Id, Assert.Single(visible.Listings).Source.Id);
        Assert.Equal(image.ImageUrl, Assert.Single(visible.Images).ImageUrl);
        Assert.Equal(source.Id, Assert.Single(visible.Images).Source!.Id);
        profile.SetStatus(GearPublicationStatus.Published, now); await db.SaveChangesAsync();
        detail = await Body<GearPaddleDetail>(await Client.GetAsync($"/api/gear/paddles/{live.Id}"));
        Assert.Equal(8, Assert.Single(detail.Variants).KitchainPerformance!.Control);
        var candidate = await Candidate(source.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Approve(candidate.Id, hidden.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Approve(candidate.Id, variant.Id)).StatusCode);
    }

    [PostgresTheory]
    [InlineData("{\"type\":123,\"name\":\"Synthetic\",\"retrievedAt\":\"2026-01-01T00:00:00Z\"}")]
    [InlineData("{\"type\":\"Unknown\",\"name\":\"Synthetic\",\"retrievedAt\":\"2026-01-01T00:00:00Z\"}")]
    [InlineData("{\"type\":\"Manual\",\"name\":\"Synthetic\",\"retrievedAt\":\"2026-01-01T00:00:00Z\",\"extra\":true}")]
    [InlineData("{}")]
    public async Task Source_payload_uses_existing_string_enum_and_strict_contract(string json) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsync("/api/admin/gear/sources", new StringContent(json, Encoding.UTF8, "application/json"))).StatusCode);

    [PostgresFact]
    public async Task Invalid_candidates_missing_records_and_bad_pagination_return_safe_errors()
    {
        var input = GearImportServiceTests.Proposal((await Source()).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/admin/gear/imports", input with { Specifications = new() { ThicknessMm = -1 } }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/admin/gear/imports", input with { SchemaVersion = 9 }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.PostAsJsonAsync("/api/admin/gear/imports", input with { SourceId = Guid.NewGuid() }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Approve(Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/admin/gear/imports/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/admin/gear/imports?status=99")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/gear/paddles?offset=-1")).StatusCode);
    }
}
