namespace Kitchain.Domain.Courts;

/// <summary>A venue containing physical courts, never a playable slot or booking.</summary>
public sealed class Court
{
    private readonly List<CourtAmenity> _amenities = [];
    private Court() { }

    public Court(
        Guid id, string name, string address, string city, int numberOfCourts,
        IndoorOutdoorType indoorOutdoor, BookingMethod bookingMethod,
        CourtDataSource dataSource, CourtStatus status, DateTimeOffset createdAt,
        IEnumerable<AmenityCode>? amenities = null, string? region = null,
        decimal? latitude = null, decimal? longitude = null, string? surface = null,
        string? openingHours = null, decimal? startingPrice = null, string? currencyCode = null,
        PriceUnit? priceUnit = null, string? phone = null, string? websiteUrl = null,
        string? socialUrl = null, string? bookingUrl = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("An ID is required.", nameof(id));
        if (numberOfCourts <= 0) throw new ArgumentOutOfRangeException(nameof(numberOfCourts));
        if (!Enum.IsDefined(indoorOutdoor)) throw new ArgumentOutOfRangeException(nameof(indoorOutdoor));
        if (!Enum.IsDefined(bookingMethod)) throw new ArgumentOutOfRangeException(nameof(bookingMethod));
        if (!Enum.IsDefined(dataSource)) throw new ArgumentOutOfRangeException(nameof(dataSource));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (priceUnit.HasValue && !Enum.IsDefined(priceUnit.Value)) throw new ArgumentOutOfRangeException(nameof(priceUnit));
        if (latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        if (latitude.HasValue != longitude.HasValue) throw new ArgumentException("Supply both coordinates or neither.");
        if (startingPrice is < 0 or > 9999999999.99m ||
            (startingPrice.HasValue && decimal.Round(startingPrice.Value, 2) != startingPrice))
            throw new ArgumentOutOfRangeException(nameof(startingPrice), "Price must fit decimal(12,2).");

        var currency = OptionalText(currencyCode, 3, nameof(currencyCode))?.ToUpperInvariant();
        if (currency is not null && (currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z')))
            throw new ArgumentException("Use a three-letter currency code.", nameof(currencyCode));
        if (startingPrice.HasValue && currency is null)
            throw new ArgumentException("A known price requires a currency.", nameof(currencyCode));
        if (priceUnit.HasValue && !startingPrice.HasValue)
            throw new ArgumentException("A price unit requires a known price.", nameof(priceUnit));
        if (createdAt == default) throw new ArgumentException("A creation timestamp is required.", nameof(createdAt));

        Id = id;
        Name = RequiredText(name, 200, nameof(name));
        Address = RequiredText(address, 500, nameof(address));
        City = RequiredText(city, 100, nameof(city));
        Region = OptionalText(region, 100, nameof(region));
        NumberOfCourts = numberOfCourts;
        IndoorOutdoor = indoorOutdoor;
        Latitude = latitude;
        Longitude = longitude;
        Surface = OptionalText(surface, 100, nameof(surface));
        OpeningHours = OptionalText(openingHours, 1000, nameof(openingHours));
        StartingPrice = startingPrice;
        CurrencyCode = currency;
        PriceUnit = priceUnit;
        Phone = OptionalText(phone, 50, nameof(phone));
        WebsiteUrl = SafeUrl(websiteUrl, nameof(websiteUrl));
        SocialUrl = SafeUrl(socialUrl, nameof(socialUrl));
        BookingUrl = SafeUrl(bookingUrl, nameof(bookingUrl));
        BookingMethod = bookingMethod;
        DataSource = dataSource;
        Status = status;
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = CreatedAt;
        _amenities.AddRange((amenities ?? []).Distinct().Select(code => new CourtAmenity(id, code)));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string? Region { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public int NumberOfCourts { get; private set; }
    public IndoorOutdoorType IndoorOutdoor { get; private set; }
    public string? Surface { get; private set; }
    public string? OpeningHours { get; private set; }
    public decimal? StartingPrice { get; private set; }
    public string? CurrencyCode { get; private set; }
    public PriceUnit? PriceUnit { get; private set; }
    public string? Phone { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public string? SocialUrl { get; private set; }
    public string? BookingUrl { get; private set; }
    public BookingMethod BookingMethod { get; private set; }
    public CourtDataSource DataSource { get; private set; }
    public CourtStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<CourtAmenity> Amenities => _amenities.AsReadOnly();

    private static string RequiredText(string value, int maxLength, string parameter) =>
        OptionalText(value, maxLength, parameter) ?? throw new ArgumentException("A value is required.", parameter);

    private static string? OptionalText(string? value, int maxLength, string parameter)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maxLength) throw new ArgumentException($"Maximum length is {maxLength}.", parameter);
        return normalized;
    }

    private static string? SafeUrl(string? value, string parameter)
    {
        var text = OptionalText(value, 2048, parameter);
        if (text is null) return null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Use an absolute HTTP(S) URL without embedded credentials.", parameter);
        return uri.AbsoluteUri;
    }
}
