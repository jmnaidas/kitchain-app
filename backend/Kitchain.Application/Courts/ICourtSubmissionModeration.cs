using Kitchain.Domain.Courts;

namespace Kitchain.Application.Courts;

public sealed record CourtSubmissionSummary(
    Guid Id, string Name, string City, string Address, int NumberOfCourts,
    IndoorOutdoorType IndoorOutdoor, BookingMethod BookingMethod,
    CourtSubmissionStatus Status, DateTimeOffset SubmittedAt);

public sealed record CourtSubmissionPhotoReview(
    Guid Id, string ImageUrl, string? AltText, int DisplayOrder, bool IsPrimary, DateTimeOffset CreatedAt);

public sealed record CourtSubmissionReview(
    Guid Id, string Name, string Address, string City, string? Region,
    decimal? Latitude, decimal? Longitude, int NumberOfCourts, IndoorOutdoorType IndoorOutdoor,
    string? Surface, string? OpeningHours, decimal? StartingPrice, string? CurrencyCode,
    PriceUnit? PriceUnit, IReadOnlyList<AmenityCode> Amenities, string? Phone,
    string? WebsiteUrl, string? SocialUrl, string? BookingUrl, BookingMethod BookingMethod,
    CourtSubmissionStatus Status, DateTimeOffset SubmittedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<CourtSubmissionPhotoReview> Photos);

public enum CourtSubmissionDecision { Approve, Reject }
public enum CourtModerationOutcome { Success, NotFound, Conflict, Invalid }
public sealed record CourtModerationReceipt(Guid SubmissionId, Guid? CourtId, CourtSubmissionStatus SubmissionStatus, DateTimeOffset UpdatedAt);
public sealed record CourtModerationResult(CourtModerationOutcome Outcome, CourtModerationReceipt? Receipt = null);

public interface ICourtSubmissionModeration
{
    Task<IReadOnlyList<CourtSubmissionSummary>> ListAsync(CourtSubmissionStatus status, CancellationToken cancellationToken);
    Task<CourtSubmissionReview?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<CourtModerationResult> DecideAsync(Guid id, CourtSubmissionDecision decision, CancellationToken cancellationToken);
}
