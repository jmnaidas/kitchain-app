namespace Kitchain.Domain.Gear;

// Every non-null field in one record shares its source, evidence type and observation time.
// Another source/observation is another record, never an overwrite or preferred-value cache.
public sealed class GearSpecifications
{
    private GearSpecifications() { }
    public GearSpecifications(decimal? thicknessMm = null, decimal? advertisedWeightMinOz = null,
        decimal? advertisedWeightMaxOz = null, decimal? lengthInches = null, decimal? widthInches = null,
        decimal? handleLengthInches = null, decimal? gripCircumferenceInches = null,
        string? faceMaterial = null, string? coreMaterial = null, string? construction = null,
        PaddleShape? shape = null, decimal? swingWeightKgCm2 = null, decimal? twistWeightKgCm2 = null,
        decimal? measuredWeightOz = null)
    {
        ThicknessMm = GearRules.Positive(thicknessMm, nameof(thicknessMm));
        AdvertisedWeightMinOz = GearRules.Positive(advertisedWeightMinOz, nameof(advertisedWeightMinOz));
        AdvertisedWeightMaxOz = GearRules.Positive(advertisedWeightMaxOz, nameof(advertisedWeightMaxOz));
        if (advertisedWeightMinOz > advertisedWeightMaxOz) throw new ArgumentException("Minimum weight cannot exceed maximum weight.");
        LengthInches = GearRules.Positive(lengthInches, nameof(lengthInches));
        WidthInches = GearRules.Positive(widthInches, nameof(widthInches));
        HandleLengthInches = GearRules.Positive(handleLengthInches, nameof(handleLengthInches));
        GripCircumferenceInches = GearRules.Positive(gripCircumferenceInches, nameof(gripCircumferenceInches));
        if (handleLengthInches > lengthInches) throw new ArgumentException("Handle cannot be longer than the paddle.");
        FaceMaterial = GearRules.Optional(faceMaterial, 160, nameof(faceMaterial));
        CoreMaterial = GearRules.Optional(coreMaterial, 160, nameof(coreMaterial));
        Construction = GearRules.Optional(construction, 240, nameof(construction));
        Shape = shape.HasValue ? GearRules.EnumValue(shape.Value) : null;
        SwingWeightKgCm2 = GearRules.Positive(swingWeightKgCm2, nameof(swingWeightKgCm2));
        TwistWeightKgCm2 = GearRules.Positive(twistWeightKgCm2, nameof(twistWeightKgCm2));
        MeasuredWeightOz = GearRules.Positive(measuredWeightOz, nameof(measuredWeightOz));
    }
    public decimal? ThicknessMm { get; private set; }
    public decimal? AdvertisedWeightMinOz { get; private set; }
    public decimal? AdvertisedWeightMaxOz { get; private set; }
    public decimal? LengthInches { get; private set; }
    public decimal? WidthInches { get; private set; }
    public decimal? HandleLengthInches { get; private set; }
    public decimal? GripCircumferenceInches { get; private set; }
    public string? FaceMaterial { get; private set; }
    public string? CoreMaterial { get; private set; }
    public string? Construction { get; private set; }
    public PaddleShape? Shape { get; private set; }
    public decimal? SwingWeightKgCm2 { get; private set; }
    public decimal? TwistWeightKgCm2 { get; private set; }
    public decimal? MeasuredWeightOz { get; private set; }
}

public sealed class PaddleSpecificationEvidence
{
    private PaddleSpecificationEvidence() { }
    public PaddleSpecificationEvidence(Guid id, Guid variantId, Guid sourceId, GearEvidenceType type,
        GearSpecifications specifications, DateTimeOffset observedAt, DateTimeOffset now)
    {
        Id = GearRules.Id(id); PaddleVariantId = GearRules.Id(variantId); SourceId = GearRules.Id(sourceId);
        Type = GearRules.EnumValue(type); Specifications = specifications ?? throw new ArgumentNullException(nameof(specifications));
        ObservedAt = GearRules.Time(observedAt); CreatedAt = GearRules.After(now, ObservedAt);
    }
    public Guid Id { get; private set; }
    public Guid PaddleVariantId { get; private set; }
    public Guid SourceId { get; private set; }
    public GearEvidenceType Type { get; private set; }
    public GearSpecifications Specifications { get; private set; } = null!;
    public DateTimeOffset ObservedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
