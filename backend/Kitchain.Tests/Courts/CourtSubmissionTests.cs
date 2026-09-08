using Kitchain.Application.Courts;
using Kitchain.Domain.Courts;

namespace Kitchain.Tests.Courts;

public sealed class CourtSubmissionTests
{
    private static readonly CreateCourtSubmission Valid = new()
    {
        Name = " Community venue ", City = "Makati", Address = "Test address",
        NumberOfCourts = 2, IndoorOutdoor = IndoorOutdoorType.Mixed, BookingMethod = BookingMethod.WalkIn
    };

    [Fact]
    public async Task Submission_normalizes_metadata_and_defaults_to_pending_without_photos()
    {
        var writer = new RecordingWriter();
        var receipt = await new CourtSubmissionService(writer).SubmitAsync(Valid with
        {
            Amenities = [AmenityCode.Parking, AmenityCode.Parking], StartingPrice = 0, CurrencyCode = "php"
        }, default);
        var saved = Assert.IsType<CourtSubmission>(writer.Saved);
        Assert.Equal(CourtSubmissionStatus.Pending, receipt.Status);
        Assert.Equal(saved.Id, receipt.Id);
        Assert.Equal("Community venue", saved.Name);
        Assert.Equal("PHP", saved.CurrencyCode);
        Assert.Single(saved.Amenities);
        Assert.Empty(saved.Photos);
        Assert.Equal(saved.SubmittedAt, saved.UpdatedAt);
    }

    [Fact]
    public async Task Invalid_booking_contact_urls_and_prices_do_not_reach_persistence()
    {
        var writer = new RecordingWriter();
        var service = new CourtSubmissionService(writer);
        foreach (var input in new[]
        {
            Valid with { Name = " " },
            Valid with { BookingMethod = BookingMethod.Phone },
            Valid with { BookingMethod = BookingMethod.GoogleForm },
            Valid with { WebsiteUrl = "javascript:alert(1)" },
            Valid with { SocialUrl = "https://user:password@example.com" },
            Valid with { StartingPrice = 1.001m, CurrencyCode = "PHP" },
            Valid with { NumberOfCourts = 0 },
            Valid with { Latitude = 14, Longitude = null },
            Valid with { Photos = [new("/relative.jpg", null)] }
        })
            await Assert.ThrowsAnyAsync<ArgumentException>(() => service.SubmitAsync(input, default));
        Assert.Null(writer.Saved);
    }

    private sealed class RecordingWriter : ICourtSubmissionWriter
    {
        public CourtSubmission? Saved { get; private set; }
        public Task AddAsync(CourtSubmission submission, CancellationToken cancellationToken)
        {
            Saved = submission;
            return Task.CompletedTask;
        }
    }
}
