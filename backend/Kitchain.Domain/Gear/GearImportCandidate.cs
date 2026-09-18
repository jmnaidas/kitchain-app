namespace Kitchain.Domain.Gear;

// A single normalized proposal is the review unit in G1. No unvalidated JSON drives publication.
public sealed class GearImportCandidate
{
    private GearImportCandidate() { }
    public GearImportCandidate(Guid id, Guid sourceId, string brandName, string brandSlug,
        string paddleName, string paddleSlug, string variantName, string variantSlug,
        string? manufacturerSku, GearEvidenceType evidenceType, GearSpecifications specifications,
        DateTimeOffset observedAt, DateTimeOffset now)
    {
        Id = GearRules.Id(id); SourceId = GearRules.Id(sourceId);
        BrandName = GearRules.Text(brandName, 120, nameof(brandName)); BrandSlug = GearRules.Slug(brandSlug);
        PaddleName = GearRules.Text(paddleName, 200, nameof(paddleName)); PaddleSlug = GearRules.Slug(paddleSlug);
        VariantName = GearRules.Text(variantName, 120, nameof(variantName)); VariantSlug = GearRules.Slug(variantSlug);
        ManufacturerSku = GearRules.Optional(manufacturerSku, 100, nameof(manufacturerSku));
        EvidenceType = GearRules.EnumValue(evidenceType);
        Specifications = specifications ?? throw new ArgumentNullException(nameof(specifications));
        ObservedAt = GearRules.Time(observedAt); CreatedAt = GearRules.After(now, ObservedAt);
    }
    public Guid Id { get; private set; }
    public Guid SourceId { get; private set; }
    public string BrandName { get; private set; } = "";
    public string BrandSlug { get; private set; } = "";
    public string PaddleName { get; private set; } = "";
    public string PaddleSlug { get; private set; } = "";
    public string VariantName { get; private set; } = "";
    public string VariantSlug { get; private set; } = "";
    public string? ManufacturerSku { get; private set; }
    public GearEvidenceType EvidenceType { get; private set; }
    public GearSpecifications Specifications { get; private set; } = null!;
    public DateTimeOffset ObservedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public GearImportStatus Status { get; private set; }
    public Guid? MatchedVariantId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewNotes { get; private set; }
    public void Decide(Guid? approvedVariantId, string? notes, DateTimeOffset now)
    {
        if (Status != GearImportStatus.Pending) throw new GearConflictException("This candidate has already been reviewed.");
        var timestamp = GearRules.After(now, CreatedAt);
        var reviewNotes = GearRules.Optional(notes, 2000, nameof(notes));
        if (approvedVariantId.HasValue) GearRules.Id(approvedVariantId.Value);
        MatchedVariantId = approvedVariantId; ReviewNotes = reviewNotes; ReviewedAt = timestamp;
        Status = approvedVariantId.HasValue ? GearImportStatus.Approved : GearImportStatus.Rejected;
    }
}
