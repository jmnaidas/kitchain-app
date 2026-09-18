namespace Kitchain.Domain.Gear;

public sealed class GearBrand
{
    private GearBrand() { }
    public GearBrand(Guid id, string name, string slug, DateTimeOffset now, string? websiteUrl = null)
    {
        Id = GearRules.Id(id); Name = GearRules.Text(name, 120, nameof(name));
        NormalizedName = GearRules.Key(Name); Slug = GearRules.Slug(slug);
        WebsiteUrl = GearRules.Url(websiteUrl); CreatedAt = UpdatedAt = GearRules.Time(now);
    }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public string? WebsiteUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
