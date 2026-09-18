namespace Kitchain.Domain.Gear;

public sealed class GearDataSource
{
    private GearDataSource() { }
    public GearDataSource(Guid id, GearSourceType type, string name, string? url, DateTimeOffset retrievedAt, DateTimeOffset now)
    {
        Id = GearRules.Id(id); Type = GearRules.EnumValue(type); Name = GearRules.Text(name, 200, nameof(name));
        Url = GearRules.Url(url);
        if (Url is null && type != GearSourceType.Manual) throw new ArgumentException("External sources require a URL.", nameof(url));
        PublisherDomain = Url is null ? null : new Uri(Url).IdnHost.ToLowerInvariant();
        RetrievedAt = LastCheckedAt = GearRules.Time(retrievedAt); CreatedAt = GearRules.After(now, RetrievedAt);
    }
    public Guid Id { get; private set; }
    public GearSourceType Type { get; private set; }
    public string Name { get; private set; } = "";
    public string? Url { get; private set; }
    public string? PublisherDomain { get; private set; }
    public DateTimeOffset RetrievedAt { get; private set; }
    public DateTimeOffset LastCheckedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public void Checked(DateTimeOffset now) => LastCheckedAt = GearRules.After(now, LastCheckedAt);
}
