using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Kitchain.Domain.Gear;

namespace Kitchain.Application.Gear;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateGearSource
{
    [Required] public GearSourceType? Type { get; init; }
    [Required] public string Name { get; init; } = "";
    public string? Url { get; init; }
    [Required] public DateTimeOffset? RetrievedAt { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GearSpecificationInput
{
    public decimal? ThicknessMm { get; init; }
    public decimal? AdvertisedWeightMinOz { get; init; }
    public decimal? AdvertisedWeightMaxOz { get; init; }
    public decimal? LengthInches { get; init; }
    public decimal? WidthInches { get; init; }
    public decimal? HandleLengthInches { get; init; }
    public decimal? GripCircumferenceInches { get; init; }
    public string? FaceMaterial { get; init; }
    public string? CoreMaterial { get; init; }
    public string? Construction { get; init; }
    public PaddleShape? Shape { get; init; }
    public decimal? SwingWeightKgCm2 { get; init; }
    public decimal? TwistWeightKgCm2 { get; init; }
    public decimal? MeasuredWeightOz { get; init; }
    public GearSpecifications Validate() => new(ThicknessMm, AdvertisedWeightMinOz, AdvertisedWeightMaxOz,
        LengthInches, WidthInches, HandleLengthInches, GripCircumferenceInches, FaceMaterial, CoreMaterial,
        Construction, Shape, SwingWeightKgCm2, TwistWeightKgCm2, MeasuredWeightOz);
    public static GearSpecificationInput From(GearSpecifications s) => new()
    {
        ThicknessMm = s.ThicknessMm,
        AdvertisedWeightMinOz = s.AdvertisedWeightMinOz,
        AdvertisedWeightMaxOz = s.AdvertisedWeightMaxOz,
        LengthInches = s.LengthInches,
        WidthInches = s.WidthInches,
        HandleLengthInches = s.HandleLengthInches,
        GripCircumferenceInches = s.GripCircumferenceInches,
        FaceMaterial = s.FaceMaterial,
        CoreMaterial = s.CoreMaterial,
        Construction = s.Construction,
        Shape = s.Shape,
        SwingWeightKgCm2 = s.SwingWeightKgCm2,
        TwistWeightKgCm2 = s.TwistWeightKgCm2,
        MeasuredWeightOz = s.MeasuredWeightOz
    };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateGearImport
{
    public int SchemaVersion { get; init; } = 1;
    [Required] public Guid? SourceId { get; init; }
    [Required] public string BrandName { get; init; } = "";
    [Required] public string BrandSlug { get; init; } = "";
    [Required] public string PaddleName { get; init; } = "";
    [Required] public string PaddleSlug { get; init; } = "";
    [Required] public string VariantName { get; init; } = "";
    [Required] public string VariantSlug { get; init; } = "";
    public string? ManufacturerSku { get; init; }
    [Required] public GearEvidenceType? EvidenceType { get; init; }
    [Required] public DateTimeOffset? ObservedAt { get; init; }
    [Required] public GearSpecificationInput Specifications { get; init; } = new();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApproveGearImport(Guid? TargetVariantId = null, string? Notes = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RejectGearImport(string? Notes = null);

public sealed record GearSourceDetail(Guid Id, GearSourceType Type, string Name, string? Url,
    string? PublisherDomain, DateTimeOffset RetrievedAt, DateTimeOffset LastCheckedAt);
public sealed record GearImportSummary(Guid Id, Guid SourceId, string BrandName, string PaddleName,
    string VariantName, GearImportStatus Status, DateTimeOffset CreatedAt);
public sealed record GearImportDetail(Guid Id, Guid SourceId, string BrandName, string BrandSlug,
    string PaddleName, string PaddleSlug, string VariantName, string VariantSlug, string? ManufacturerSku,
    GearEvidenceType EvidenceType, GearSpecificationInput Specifications, DateTimeOffset ObservedAt,
    GearImportStatus Status, Guid? MatchedVariantId, string? ReviewNotes, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt);

public sealed record GearPublication(GearBrand? Brand, Paddle? Paddle, PaddleVariant? Variant, PaddleSpecificationEvidence Evidence);

public interface IGearImportStore
{
    Task AddSourceAsync(GearDataSource source, CancellationToken ct);
    Task<GearDataSource?> FindSourceAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<GearDataSource>> SourcesAsync(int offset, CancellationToken ct);
    Task AddCandidateAsync(GearImportCandidate candidate, CancellationToken ct);
    Task<IReadOnlyList<GearImportCandidate>> ListAsync(GearImportStatus status, int offset, CancellationToken ct);
    Task<GearImportCandidate?> FindAsync(Guid id, CancellationToken ct);
    // The row lock and transaction enclose matching, staging, candidate decision and save.
    Task<GearImportCandidate> ReviewAsync(Guid id, Func<GearImportCandidate, CancellationToken, Task> review, CancellationToken ct);
    Task<GearBrand?> MatchBrandAsync(string slug, string normalizedName, CancellationToken ct);
    Task<Paddle?> MatchPaddleAsync(Guid brandId, string slug, CancellationToken ct);
    Task<Paddle?> FindPaddleAsync(Guid id, CancellationToken ct);
    Task<PaddleVariant?> FindVariantAsync(Guid id, CancellationToken ct);
    void Stage(GearPublication publication);
}
