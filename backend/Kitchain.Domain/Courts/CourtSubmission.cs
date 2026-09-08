namespace Kitchain.Domain.Courts;

/// <summary>Community information awaiting review; never a published venue.</summary>
public sealed class CourtSubmission
{
    private readonly List<CourtSubmissionAmenity> _amenities = [];
    private readonly List<CourtSubmissionPhoto> _photos = [];
    private CourtSubmission() { }

    public CourtSubmission(
        Guid id, string name, string address, string city, int numberOfCourts,
        IndoorOutdoorType indoorOutdoor, BookingMethod bookingMethod,
        DateTimeOffset submittedAt,
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
        if (submittedAt == default) throw new ArgumentException("A creation timestamp is required.", nameof(submittedAt));

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
        if (bookingMethod is BookingMethod.ExternalPlatform or BookingMethod.GoogleForm && BookingUrl is null)
            throw new ArgumentException("A booking URL is required for this booking method.", nameof(bookingUrl));
        if (bookingMethod == BookingMethod.Website && WebsiteUrl is null && BookingUrl is null)
            throw new ArgumentException("Supply a website or booking URL.", nameof(websiteUrl));
        if (bookingMethod == BookingMethod.Phone && Phone is null)
            throw new ArgumentException("Supply a phone number.", nameof(phone));
        if (bookingMethod == BookingMethod.Message && SocialUrl is null && Phone is null)
            throw new ArgumentException("Supply a social link or phone number.", nameof(socialUrl));
        if (bookingMethod == BookingMethod.Other && Phone is null && WebsiteUrl is null && SocialUrl is null && BookingUrl is null)
            throw new ArgumentException("Supply a contact or booking link.", nameof(phone));
        Status = CourtSubmissionStatus.Pending;
        SubmittedAt = submittedAt.ToUniversalTime();
        UpdatedAt = SubmittedAt;
        _amenities.AddRange((amenities ?? []).Distinct().Select(code => new CourtSubmissionAmenity(id, code)));
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
    public CourtSubmissionStatus Status { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<CourtSubmissionAmenity> Amenities => _amenities.AsReadOnly();
    public IReadOnlyCollection<CourtSubmissionPhoto> Photos => _photos.AsReadOnly();

    public void AddPhoto(CourtSubmissionPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        if (photo.CourtSubmissionId != Id) throw new ArgumentException("The photo belongs to another venue.", nameof(photo));
        if (_photos.Any(p => p.Id == photo.Id)) throw new ArgumentException("The photo ID already exists.", nameof(photo));
        if (photo.IsPrimary && _photos.Any(p => p.IsPrimary))
            throw new ArgumentException("A venue can have at most one primary photo.", nameof(photo));
        if (photo.CreatedAt < SubmittedAt) throw new ArgumentException("A photo cannot predate its venue.", nameof(photo));
        _photos.Add(photo);
        if (photo.CreatedAt > UpdatedAt) UpdatedAt = photo.CreatedAt;
    }

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
        if (uri.AbsoluteUri.Length > 2048) throw new ArgumentException("Maximum URL length is 2048.", parameter);
        return uri.AbsoluteUri;
    }
}

public enum CourtSubmissionStatus { Pending, Approved, Rejected }
