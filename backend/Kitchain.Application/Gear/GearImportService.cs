using Kitchain.Domain.Gear;

namespace Kitchain.Application.Gear;

public sealed class GearImportService(IGearImportStore store)
{
    public async Task<GearSourceDetail> CreateSourceAsync(CreateGearSource input, CancellationToken ct)
    {
        var source = new GearDataSource(Guid.NewGuid(), input.Type ?? throw new ArgumentException("Source type is required."),
            input.Name, input.Url, input.RetrievedAt ?? throw new ArgumentException("Retrieved time is required."), DateTimeOffset.UtcNow);
        await store.AddSourceAsync(source, ct); return Source(source);
    }
    public async Task<IReadOnlyList<GearSourceDetail>> SourcesAsync(int offset, CancellationToken ct) =>
        (await store.SourcesAsync(Offset(offset), ct)).Select(Source).ToArray();
    public async Task<GearImportDetail> CreateAsync(CreateGearImport input, CancellationToken ct)
    {
        if (input.SchemaVersion != 1) throw new ArgumentException("Only schemaVersion 1 is supported.", "schemaVersion");
        var sourceId = GearRules.Id(input.SourceId ?? throw new ArgumentException("Source is required."));
        if (await store.FindSourceAsync(sourceId, ct) is null) throw new KeyNotFoundException("Source not found.");
        var candidate = new GearImportCandidate(Guid.NewGuid(), sourceId, input.BrandName, input.BrandSlug,
            input.PaddleName, input.PaddleSlug, input.VariantName, input.VariantSlug, input.ManufacturerSku,
            input.EvidenceType ?? throw new ArgumentException("Evidence type is required."),
            input.Specifications?.Validate() ?? throw new ArgumentException("Specifications object is required."),
            input.ObservedAt ?? throw new ArgumentException("Observation time is required."), DateTimeOffset.UtcNow);
        await store.AddCandidateAsync(candidate, ct); return Detail(candidate);
    }
    public async Task<IReadOnlyList<GearImportSummary>> ListAsync(GearImportStatus status, int offset, CancellationToken ct)
    {
        GearRules.EnumValue(status);
        return (await store.ListAsync(status, Offset(offset), ct)).Select(c => new GearImportSummary(c.Id, c.SourceId,
            c.BrandName, c.PaddleName, c.VariantName, c.Status, c.CreatedAt)).ToArray();
    }
    public async Task<GearImportDetail?> FindAsync(Guid id, CancellationToken ct)
    {
        var candidate = await store.FindAsync(id, ct); return candidate is null ? null : Detail(candidate);
    }
    public async Task<GearImportDetail> ApproveAsync(Guid id, ApproveGearImport input, CancellationToken ct) =>
        Detail(await store.ReviewAsync(id, async (candidate, token) =>
        {
            if (candidate.Status != GearImportStatus.Pending) throw new GearConflictException("This candidate has already been reviewed.");
            var now = DateTimeOffset.UtcNow;
            if (now < candidate.CreatedAt) now = candidate.CreatedAt;
            GearBrand? newBrand = null; Paddle? newPaddle = null; PaddleVariant? newVariant = null;
            Guid variantId;
            if (input.TargetVariantId.HasValue)
            {
                var target = await store.FindVariantAsync(GearRules.Id(input.TargetVariantId.Value), token)
                    ?? throw new KeyNotFoundException("Variant not found.");
                var parent = await store.FindPaddleAsync(target.PaddleId, token) ?? throw new KeyNotFoundException();
                if (target.Status != GearPublicationStatus.Published || parent.Status != GearPublicationStatus.Published)
                    throw new GearConflictException("Evidence can only be attached to a Published paddle and variant.");
                variantId = target.Id;
            }
            else
            {
                var brand = await store.MatchBrandAsync(candidate.BrandSlug, GearRules.Key(candidate.BrandName), token);
                if (brand is null) brand = newBrand = new(Guid.NewGuid(), candidate.BrandName, candidate.BrandSlug, now);
                else if (brand.Slug != candidate.BrandSlug || brand.NormalizedName != GearRules.Key(candidate.BrandName))
                    throw new GearConflictException("Brand identity conflicts. Review the name and slug before importing.");
                var paddle = await store.MatchPaddleAsync(brand.Id, candidate.PaddleSlug, token);
                if (paddle is null)
                {
                    paddle = newPaddle = new(Guid.NewGuid(), brand.Id, candidate.PaddleName, candidate.PaddleSlug, now);
                    paddle.SetStatus(GearPublicationStatus.Published, now);
                }
                else if (paddle.Status != GearPublicationStatus.Published || GearRules.Key(paddle.Name) != GearRules.Key(candidate.PaddleName))
                    throw new GearConflictException("Paddle identity or publishing state conflicts. No existing product was changed.");
                newVariant = new(Guid.NewGuid(), paddle.Id, candidate.VariantName, candidate.VariantSlug, now, candidate.ManufacturerSku);
                newVariant.SetStatus(GearPublicationStatus.Published, now); variantId = newVariant.Id;
            }
            // Revalidate typed proposals and copy the owned value; never deserialize raw JSON into entities.
            var evidence = new PaddleSpecificationEvidence(Guid.NewGuid(), variantId, candidate.SourceId,
                candidate.EvidenceType, GearSpecificationInput.From(candidate.Specifications).Validate(), candidate.ObservedAt, now);
            candidate.Decide(variantId, input.Notes, now);
            store.Stage(new(newBrand, newPaddle, newVariant, evidence));
        }, ct));
    public async Task<GearImportDetail> RejectAsync(Guid id, RejectGearImport input, CancellationToken ct) =>
        Detail(await store.ReviewAsync(id, (candidate, _) =>
        {
            candidate.Decide(null, input.Notes, DateTimeOffset.UtcNow < candidate.CreatedAt ? candidate.CreatedAt : DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }, ct));
    private static int Offset(int value) => value is >= 0 and <= 100000 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static GearSourceDetail Source(GearDataSource s) => new(s.Id, s.Type, s.Name, s.Url, s.PublisherDomain, s.RetrievedAt, s.LastCheckedAt);
    private static GearImportDetail Detail(GearImportCandidate c) => new(c.Id, c.SourceId, c.BrandName, c.BrandSlug,
        c.PaddleName, c.PaddleSlug, c.VariantName, c.VariantSlug, c.ManufacturerSku, c.EvidenceType,
        GearSpecificationInput.From(c.Specifications), c.ObservedAt, c.Status, c.MatchedVariantId, c.ReviewNotes, c.CreatedAt, c.ReviewedAt);
}
