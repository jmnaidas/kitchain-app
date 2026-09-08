using Kitchain.Domain.Courts;

namespace Kitchain.Tests.Courts;

public sealed class CourtDomainTests
{
    private static Court Create(string name = "Venue", string city = "Makati", string address = "Address",
        int count = 2, decimal? latitude = null, decimal? longitude = null,
        decimal? price = 200m, string? currency = "PHP", string? url = null,
        IndoorOutdoorType type = IndoorOutdoorType.Indoor, AmenityCode[]? amenities = null) =>
        new(Guid.NewGuid(), name, address, city, count, type, BookingMethod.WalkIn,
            CourtDataSource.KitchainCurated, CourtStatus.Published, DateTimeOffset.UtcNow,
            amenities, latitude: latitude, longitude: longitude, startingPrice: price,
            currencyCode: currency, websiteUrl: url);

    [Theory]
    [InlineData("", "Makati", "Address")]
    [InlineData("Venue", " ", "Address")]
    [InlineData("Venue", "Makati", "")]
    public void Required_venue_information_cannot_be_blank(string name, string city, string address) =>
        Assert.Throws<ArgumentException>(() => Create(name, city, address));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Physical_court_count_must_be_positive(int count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(count: count));

    [Theory]
    [InlineData(91, 121)]
    [InlineData(-91, 121)]
    [InlineData(14, 181)]
    [InlineData(14, -181)]
    public void Coordinates_must_be_in_range(int latitude, int longitude) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(latitude: latitude, longitude: longitude));

    [Fact]
    public void Coordinates_must_be_supplied_as_a_pair() =>
        Assert.Throws<ArgumentException>(() => Create(latitude: 14));

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("10000000000")]
    public void Price_must_fit_nonnegative_decimal_money(string value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(price: decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData(null)]
    [InlineData("P1P")]
    [InlineData("PH")]
    public void Known_price_requires_three_letter_currency(string? currency) =>
        Assert.Throws<ArgumentException>(() => Create(currency: currency));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///tmp/test")]
    [InlineData("/relative")]
    [InlineData("https://user:password@example.com")]
    public void Unsafe_or_relative_urls_are_rejected(string url) =>
        Assert.Throws<ArgumentException>(() => Create(url: url));

    [Fact]
    public void Normalizes_text_currency_urls_and_deduplicates_amenities()
    {
        var court = Create(name: " Venue ", city: " Makati ", currency: " php ",
            url: " HTTPS://EXAMPLE.COM ", amenities: [AmenityCode.Parking, AmenityCode.Parking]);
        Assert.Equal("Venue", court.Name);
        Assert.Equal("Makati", court.City);
        Assert.Equal("PHP", court.CurrencyCode);
        Assert.Equal("https://example.com/", court.WebsiteUrl);
        Assert.Single(court.Amenities);
        Assert.Equal(court.CreatedAt, court.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, court.CreatedAt.Offset);
    }

    [Fact]
    public void Unknown_price_and_coordinates_are_allowed_without_invented_values()
    {
        var court = Create(price: null, currency: null);
        Assert.Null(court.StartingPrice);
        Assert.Null(court.Latitude);
        Assert.Null(court.Longitude);
    }

    [Fact]
    public void Unknown_enum_values_are_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(type: (IndoorOutdoorType)99));
}
