using Kitchain.Application.Gear;
using Kitchain.Domain.Gear;

namespace Kitchain.Tests.Gear;

public sealed class GearImportServiceTests
{
    internal static CreateGearImport Proposal(Guid sourceId, string suffix = "") => new()
    {
        SourceId = sourceId,
        BrandName = "Kitchain Labs" + suffix,
        BrandSlug = "kitchain-labs" + suffix,
        PaddleName = "Baseline One",
        PaddleSlug = "baseline-one",
        VariantName = "16 mm",
        VariantSlug = "16-mm",
        EvidenceType = GearEvidenceType.ManufacturerStated,
        ObservedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        Specifications = new() { ThicknessMm = 16, AdvertisedWeightMinOz = 8, AdvertisedWeightMaxOz = 8.3m }
    };

    [Fact]
    public async Task Create_list_find_approve_retain_validated_proposal_and_provenance()
    {
        var store = new MemoryStore(); var service = new GearImportService(store);
        var input = Proposal(store.Source.Id) with { BrandName = " Kitchain Labs ", BrandSlug = "KITCHAIN-LABS" };
        var candidate = await service.CreateAsync(input, default);
        Assert.Equal("Kitchain Labs", candidate.BrandName);
        Assert.Equal("kitchain-labs", candidate.BrandSlug);
        Assert.Single(await service.ListAsync(GearImportStatus.Pending, 0, default));
        Assert.Equal(candidate, await service.FindAsync(candidate.Id, default));
        var approved = await service.ApproveAsync(candidate.Id, new(Notes: "Synthetic review"), default);
        Assert.Equal(GearImportStatus.Approved, approved.Status);
        var publication = Assert.Single(store.Publications);
        Assert.Equal(GearPublicationStatus.Published, publication.Paddle!.Status);
        Assert.Equal(GearPublicationStatus.Published, publication.Variant!.Status);
        Assert.Equal(store.Source.Id, publication.Evidence.SourceId);
        Assert.Equal(input.ObservedAt, publication.Evidence.ObservedAt);
        Assert.Equal(16, publication.Evidence.Specifications.ThicknessMm);
        Assert.Empty(await service.ListAsync(GearImportStatus.Pending, 0, default));
        await Assert.ThrowsAsync<GearConflictException>(() => service.ApproveAsync(candidate.Id, new(), default));
    }

    [Fact]
    public async Task Invalid_proposals_and_missing_sources_never_enter_review()
    {
        var store = new MemoryStore(); var service = new GearImportService(store); var input = Proposal(store.Source.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(input with { Specifications = new() { ThicknessMm = -1 } }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(input with { SchemaVersion = 2 }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(input with { EvidenceType = (GearEvidenceType)99 }, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(input with { SourceId = Guid.NewGuid() }, default));
        Assert.Empty(store.Candidates); Assert.Empty(store.Publications);
    }

    [Fact]
    public async Task Rejection_never_publishes_and_cannot_be_reversed()
    {
        var store = new MemoryStore(); var service = new GearImportService(store);
        var candidate = await service.CreateAsync(Proposal(store.Source.Id), default);
        var rejected = await service.RejectAsync(candidate.Id, new("Insufficient synthetic evidence"), default);
        Assert.Equal(GearImportStatus.Rejected, rejected.Status); Assert.Null(rejected.MatchedVariantId);
        Assert.Empty(store.Publications);
        await Assert.ThrowsAsync<GearConflictException>(() => service.ApproveAsync(candidate.Id, new(), default));
        await Assert.ThrowsAsync<GearConflictException>(() => service.RejectAsync(candidate.Id, new(), default));
    }

    [Fact]
    public async Task Explicit_match_appends_conflicting_evidence_without_replacing_identity_or_facts()
    {
        var store = new MemoryStore(); var service = new GearImportService(store);
        var first = await service.CreateAsync(Proposal(store.Source.Id), default);
        var approved = await service.ApproveAsync(first.Id, new(), default);
        var second = await service.CreateAsync(Proposal(store.Source.Id) with
        {
            PaddleName = "Extracted alias",
            EvidenceType = GearEvidenceType.IndependentlyMeasured,
            Specifications = new() { ThicknessMm = 15.9m, MeasuredWeightOz = 8.27m }
        }, default);
        await service.ApproveAsync(second.Id, new(approved.MatchedVariantId), default);
        Assert.Equal(2, store.Publications.Count);
        var added = store.Publications[1];
        Assert.Null(added.Brand); Assert.Null(added.Paddle); Assert.Null(added.Variant);
        Assert.Equal(approved.MatchedVariantId, added.Evidence.PaddleVariantId);
        Assert.Equal(15.9m, added.Evidence.Specifications.ThicknessMm);
        Assert.Equal(16m, store.Publications[0].Evidence.Specifications.ThicknessMm);
        Assert.Equal("Baseline One", store.Publications[0].Paddle!.Name);
    }

    // Transaction/uniqueness behavior is intentionally tested against PostgreSQL, not simulated here.
    private sealed class MemoryStore : IGearImportStore
    {
        public GearDataSource Source { get; } = new(Guid.NewGuid(), GearSourceType.Manual, "Synthetic test source", null,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        public List<GearImportCandidate> Candidates { get; } = [];
        public List<GearPublication> Publications { get; } = [];
        public Task AddSourceAsync(GearDataSource source, CancellationToken ct) => Task.CompletedTask;
        public Task<GearDataSource?> FindSourceAsync(Guid id, CancellationToken ct) => Task.FromResult(id == Source.Id ? Source : null);
        public Task<IReadOnlyList<GearDataSource>> SourcesAsync(int offset, CancellationToken ct) => Task.FromResult<IReadOnlyList<GearDataSource>>([Source]);
        public Task AddCandidateAsync(GearImportCandidate candidate, CancellationToken ct) { Candidates.Add(candidate); return Task.CompletedTask; }
        public Task<IReadOnlyList<GearImportCandidate>> ListAsync(GearImportStatus status, int offset, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GearImportCandidate>>(Candidates.Where(c => c.Status == status).Skip(offset).ToArray());
        public Task<GearImportCandidate?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Candidates.SingleOrDefault(c => c.Id == id));
        public async Task<GearImportCandidate> ReviewAsync(Guid id, Func<GearImportCandidate, CancellationToken, Task> review, CancellationToken ct)
        { var c = Candidates.Single(c => c.Id == id); await review(c, ct); return c; }
        public Task<GearBrand?> MatchBrandAsync(string slug, string normalizedName, CancellationToken ct) =>
            Task.FromResult(Publications.Select(p => p.Brand).FirstOrDefault(b => b?.Slug == slug || b?.NormalizedName == normalizedName));
        public Task<Paddle?> MatchPaddleAsync(Guid brandId, string slug, CancellationToken ct) =>
            Task.FromResult(Publications.Select(p => p.Paddle).FirstOrDefault(p => p?.BrandId == brandId && p.Slug == slug));
        public Task<Paddle?> FindPaddleAsync(Guid id, CancellationToken ct) => Task.FromResult(Publications.Select(p => p.Paddle).FirstOrDefault(p => p?.Id == id));
        public Task<PaddleVariant?> FindVariantAsync(Guid id, CancellationToken ct) => Task.FromResult(Publications.Select(p => p.Variant).FirstOrDefault(v => v?.Id == id));
        public void Stage(GearPublication publication) => Publications.Add(publication);
    }
}
