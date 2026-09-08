using Kitchain.Domain.Courts;

namespace Kitchain.Application.Courts;

public enum AvailabilityIntegrationStatus { NotIntegrated }

public sealed record CourtAvailability(AvailabilityIntegrationStatus Status, string Message)
{
    public static CourtAvailability NotIntegrated { get; } = new(
        AvailabilityIntegrationStatus.NotIntegrated,
        "Availability is not integrated. Contact the venue or use its external booking method.");
}

public sealed record CourtSummary(
    Guid Id, string Name, string City, string Address, int NumberOfCourts,
    IndoorOutdoorType IndoorOutdoor, decimal? StartingPrice, string? CurrencyCode,
    PriceUnit? PriceUnit, IReadOnlyList<AmenityCode> Amenities, BookingMethod BookingMethod,
    CourtDataSource DataSource)
{
    public CourtAvailability Availability => CourtAvailability.NotIntegrated;
}

public sealed record CourtDetail(
    Guid Id, string Name, string Address, string City, string? Region,
    decimal? Latitude, decimal? Longitude, int NumberOfCourts, IndoorOutdoorType IndoorOutdoor,
    string? Surface, string? OpeningHours, decimal? StartingPrice, string? CurrencyCode,
    PriceUnit? PriceUnit, IReadOnlyList<AmenityCode> Amenities, string? Phone,
    string? WebsiteUrl, string? SocialUrl, string? BookingUrl, BookingMethod BookingMethod,
    CourtDataSource DataSource, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public CourtAvailability Availability => CourtAvailability.NotIntegrated;
}

public sealed record CourtPage(IReadOnlyList<CourtSummary> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
