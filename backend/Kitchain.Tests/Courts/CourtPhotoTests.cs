using Kitchain.Domain.Courts;

namespace Kitchain.Tests.Courts;

public sealed class CourtPhotoTests
{
    private static Court Venue() => new(Guid.NewGuid(), "Test venue", "Address", "Makati", 2,
        IndoorOutdoorType.Indoor, BookingMethod.WalkIn, CourtDataSource.KitchainCurated,
        CourtStatus.Published, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData("")]
    [InlineData("/relative.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.com/photo.jpg")]
    public void Rejects_unsafe_image_urls(string url) =>
        Assert.Throws<ArgumentException>(() => new CourtPhoto(Guid.NewGuid(), Guid.NewGuid(), url, 0, false, DateTimeOffset.UtcNow));

    [Fact]
    public void Validates_identity_order_timestamp_and_alt_text()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new CourtPhoto(Guid.Empty, id, "https://example.com/a.jpg", 0, false, now));
        Assert.Throws<ArgumentException>(() => new CourtPhoto(id, Guid.Empty, "https://example.com/a.jpg", 0, false, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CourtPhoto(id, id, "https://example.com/a.jpg", -1, false, now));
        Assert.Throws<ArgumentException>(() => new CourtPhoto(id, id, "https://example.com/a.jpg", 0, false, default));
        Assert.Throws<ArgumentException>(() => new CourtPhoto(id, id, "https://example.com/a.jpg", 0, false, now, new string('a', 501)));
    }

    [Fact]
    public void Normalizes_metadata_and_rejects_another_owner_duplicate_id_or_second_primary()
    {
        var court = Venue();
        var timestamp = new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.FromHours(8));
        var photo = new CourtPhoto(Guid.NewGuid(), court.Id, " HTTPS://EXAMPLE.COM/a.jpg ", 0, true, timestamp, " Court view ");
        court.AddPhoto(photo);
        Assert.Equal("https://example.com/a.jpg", photo.ImageUrl);
        Assert.Equal("Court view", photo.AltText);
        Assert.Equal(TimeSpan.Zero, photo.CreatedAt.Offset);
        Assert.Equal(photo.CreatedAt, court.UpdatedAt);
        Assert.Throws<ArgumentException>(() => court.AddPhoto(photo));
        Assert.Throws<ArgumentException>(() => court.AddPhoto(new CourtPhoto(Guid.NewGuid(), Guid.NewGuid(), photo.ImageUrl, 1, false, timestamp)));
        Assert.Throws<ArgumentException>(() => court.AddPhoto(new CourtPhoto(Guid.NewGuid(), court.Id, photo.ImageUrl, 1, true, timestamp)));
        Assert.Single(court.Photos);
    }
}
