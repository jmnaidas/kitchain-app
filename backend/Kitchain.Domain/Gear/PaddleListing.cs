namespace Kitchain.Domain.Gear;

// A checked commercial observation. Subsequent prices may be appended without rewriting evidence.
public sealed class PaddleListing
{
    private PaddleListing() { }
    public PaddleListing(Guid id, Guid variantId, Guid sourceId, string sellerName, string url,
        string currencyCode, decimal? price, decimal? originalPrice, bool? inStock,
        DateTimeOffset lastCheckedAt, DateTimeOffset now)
    {
        Id = GearRules.Id(id); PaddleVariantId = GearRules.Id(variantId); SourceId = GearRules.Id(sourceId);
        SellerName = GearRules.Text(sellerName, 160, nameof(sellerName));
        Url = GearRules.Url(url) ?? throw new ArgumentException("Listing URL is required.", nameof(url));
        CurrencyCode = GearRules.Text(currencyCode, 3, nameof(currencyCode)).ToUpperInvariant();
        if (CurrencyCode.Length != 3 || CurrencyCode.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Use a three-letter currency code.", nameof(currencyCode));
        Price = GearRules.Price(price); OriginalPrice = GearRules.Price(originalPrice); InStock = inStock;
        LastCheckedAt = GearRules.Time(lastCheckedAt); CreatedAt = UpdatedAt = GearRules.After(now, LastCheckedAt);
    }
    public Guid Id { get; private set; }
    public Guid PaddleVariantId { get; private set; }
    public Guid SourceId { get; private set; }
    public string SellerName { get; private set; } = "";
    public string Url { get; private set; } = "";
    public string CurrencyCode { get; private set; } = "";
    public decimal? Price { get; private set; }
    public decimal? OriginalPrice { get; private set; }
    public bool? InStock { get; private set; }
    public DateTimeOffset LastCheckedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
