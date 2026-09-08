using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Kitchain.Domain.Courts;

namespace Kitchain.Application.Courts;

// Reject server-owned fields, including status and timestamps, instead of silently accepting them.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCourtSubmission
{
    [Required] public string Name { get; init; } = "";
    [Required] public string City { get; init; } = "";
    [Required] public string Address { get; init; } = "";
    [Required] public int? NumberOfCourts { get; init; }
    [Required] public IndoorOutdoorType? IndoorOutdoor { get; init; }
    [Required] public BookingMethod? BookingMethod { get; init; }
    public string? Region { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public string? Surface { get; init; }
    public string? OpeningHours { get; init; }
    public decimal? StartingPrice { get; init; }
    public string? CurrencyCode { get; init; }
    public PriceUnit? PriceUnit { get; init; }
    public string? Phone { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? SocialUrl { get; init; }
    public string? BookingUrl { get; init; }
    [MaxLength(13)] public AmenityCode[] Amenities { get; init; } = [];
    [MaxLength(5)] public SubmissionPhotoInput[] Photos { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmissionPhotoInput(string ImageUrl, string? AltText);
public sealed record CourtSubmissionReceipt(Guid Id, CourtSubmissionStatus Status, DateTimeOffset SubmittedAt);

public interface ICourtSubmissionWriter
{
    Task AddAsync(CourtSubmission submission, CancellationToken cancellationToken);
}

public sealed class CourtSubmissionService(ICourtSubmissionWriter writer)
{
    public async Task<CourtSubmissionReceipt> SubmitAsync(CreateCourtSubmission input, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var submission = new CourtSubmission(Guid.NewGuid(), input.Name, input.Address, input.City,
            input.NumberOfCourts ?? throw new ArgumentException("Number of courts is required.", "numberOfCourts"),
            input.IndoorOutdoor ?? throw new ArgumentException("Choose a setting.", "indoorOutdoor"),
            input.BookingMethod ?? throw new ArgumentException("Choose a booking method.", "bookingMethod"), now,
            input.Amenities, input.Region, input.Latitude, input.Longitude, input.Surface, input.OpeningHours,
            input.StartingPrice, input.CurrencyCode, input.PriceUnit, input.Phone, input.WebsiteUrl, input.SocialUrl, input.BookingUrl);
        if (input.Photos is null || input.Photos.Length > 5)
            throw new ArgumentException("Supply at most five photo URLs.", "photos");
        for (var index = 0; index < input.Photos.Length; index++)
        {
            var photo = input.Photos[index];
            if (photo is null) throw new ArgumentException("Supply a photo URL.", "photos");
            submission.AddPhoto(new CourtSubmissionPhoto(Guid.NewGuid(), submission.Id, photo.ImageUrl,
                index, index == 0, now, photo.AltText));
        }
        await writer.AddAsync(submission, cancellationToken);
        return new(submission.Id, submission.Status, submission.SubmittedAt);
    }
}
