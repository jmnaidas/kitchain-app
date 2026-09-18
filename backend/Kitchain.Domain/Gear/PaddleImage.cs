namespace Kitchain.Domain.Gear;

public sealed class PaddleImage
{
    private PaddleImage() { }
    public PaddleImage(Guid id, Guid variantId, string imageUrl, int sortOrder, bool isPrimary,
        DateTimeOffset now, Guid? sourceId = null, string? altText = null)
    {
        Id = GearRules.Id(id); PaddleVariantId = GearRules.Id(variantId);
        SourceId = sourceId.HasValue ? GearRules.Id(sourceId.Value) : null;
        ImageUrl = GearRules.Url(imageUrl) ?? throw new ArgumentException("Image URL is required.", nameof(imageUrl));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        SortOrder = sortOrder; IsPrimary = isPrimary;
        AltText = GearRules.Optional(altText, 300, nameof(altText)); CreatedAt = GearRules.Time(now);
    }
    public Guid Id { get; private set; }
    public Guid PaddleVariantId { get; private set; }
    public Guid? SourceId { get; private set; }
    public string ImageUrl { get; private set; } = "";
    public string? AltText { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
