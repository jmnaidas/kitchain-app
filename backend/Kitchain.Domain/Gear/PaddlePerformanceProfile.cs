namespace Kitchain.Domain.Gear;

// Kitchain interpretation, never manufacturer evidence; no calculation algorithm in G1.
public sealed class PaddlePerformanceProfile
{
    private PaddlePerformanceProfile() { }
    public PaddlePerformanceProfile(Guid variantId, string methodVersion, DateTimeOffset now,
        int? power = null, int? control = null, int? forgiveness = null, int? handSpeed = null,
        int? spin = null, int? sweetSpot = null, PaddleStyle? overallStyle = null)
    {
        PaddleVariantId = GearRules.Id(variantId); MethodVersion = GearRules.Text(methodVersion, 80, nameof(methodVersion));
        Power = Rating(power); Control = Rating(control); Forgiveness = Rating(forgiveness);
        HandSpeed = Rating(handSpeed); Spin = Rating(spin); SweetSpot = Rating(sweetSpot);
        OverallStyle = overallStyle.HasValue ? GearRules.EnumValue(overallStyle.Value) : null;
        UpdatedAt = GearRules.Time(now);
    }
    private static int? Rating(int? value) => value is < 1 or > 10 ? throw new ArgumentException("Performance ratings must be 1–10 or unknown.") : value;
    public Guid PaddleVariantId { get; private set; }
    public string MethodVersion { get; private set; } = "";
    public int? Power { get; private set; }
    public int? Control { get; private set; }
    public int? Forgiveness { get; private set; }
    public int? HandSpeed { get; private set; }
    public int? Spin { get; private set; }
    public int? SweetSpot { get; private set; }
    public PaddleStyle? OverallStyle { get; private set; }
    public GearPublicationStatus Status { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void SetStatus(GearPublicationStatus status, DateTimeOffset now)
    {
        var timestamp = GearRules.After(now, UpdatedAt); GearRules.Transition(Status, status);
        Status = status; UpdatedAt = timestamp;
    }
}
