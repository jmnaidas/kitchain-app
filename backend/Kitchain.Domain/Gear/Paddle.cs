namespace Kitchain.Domain.Gear;

public sealed class Paddle
{
    private Paddle() { }
    public Paddle(Guid id, Guid brandId, string name, string slug, DateTimeOffset now,
        string? modelFamily = null, string? description = null, int? releaseYear = null)
    {
        Id = GearRules.Id(id); BrandId = GearRules.Id(brandId);
        Name = GearRules.Text(name, 200, nameof(name)); Slug = GearRules.Slug(slug);
        ModelFamily = GearRules.Optional(modelFamily, 120, nameof(modelFamily));
        Description = GearRules.Optional(description, 2000, nameof(description));
        if (releaseYear is < 1965 or > 2200) throw new ArgumentException("Release year must be 1965–2200.", nameof(releaseYear));
        ReleaseYear = releaseYear; CreatedAt = UpdatedAt = GearRules.Time(now);
    }
    public Guid Id { get; private set; }
    public Guid BrandId { get; private set; }
    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public string? ModelFamily { get; private set; }
    public string? Description { get; private set; }
    public int? ReleaseYear { get; private set; }
    public GearPublicationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void SetStatus(GearPublicationStatus status, DateTimeOffset now)
    {
        var timestamp = GearRules.After(now, UpdatedAt); GearRules.Transition(Status, status);
        Status = status; UpdatedAt = timestamp;
    }
}

public sealed class PaddleVariant
{
    private PaddleVariant() { }
    public PaddleVariant(Guid id, Guid paddleId, string name, string slug, DateTimeOffset now, string? manufacturerSku = null)
    {
        Id = GearRules.Id(id); PaddleId = GearRules.Id(paddleId);
        Name = GearRules.Text(name, 120, nameof(name)); Slug = GearRules.Slug(slug);
        ManufacturerSku = GearRules.Optional(manufacturerSku, 100, nameof(manufacturerSku));
        NormalizedSku = ManufacturerSku is null ? null : GearRules.Key(ManufacturerSku);
        CreatedAt = UpdatedAt = GearRules.Time(now);
    }
    public Guid Id { get; private set; }
    public Guid PaddleId { get; private set; }
    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public string? ManufacturerSku { get; private set; }
    public string? NormalizedSku { get; private set; }
    public GearPublicationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void SetStatus(GearPublicationStatus status, DateTimeOffset now)
    {
        var timestamp = GearRules.After(now, UpdatedAt); GearRules.Transition(Status, status);
        Status = status; UpdatedAt = timestamp;
    }
}
