namespace Kitchain.Domain.Courts;

/// <summary>External image metadata only. Image bytes are not owned by this entity.</summary>
public sealed class CourtSubmissionPhoto
{
    private CourtSubmissionPhoto() { }

    public CourtSubmissionPhoto(Guid id, Guid courtSubmissionId, string imageUrl, int displayOrder,
        bool isPrimary, DateTimeOffset createdAt, string? altText = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("An ID is required.", nameof(id));
        if (courtSubmissionId == Guid.Empty) throw new ArgumentException("A venue is required.", nameof(courtSubmissionId));
        if (displayOrder < 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        if (createdAt == default) throw new ArgumentException("A creation timestamp is required.", nameof(createdAt));
        var text = imageUrl?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 2048 ||
            !Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsoluteUri.Length > 2048)
            throw new ArgumentException("Use an absolute HTTP(S) image URL without credentials, up to 2048 characters.", nameof(imageUrl));
        var alt = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        if (alt?.Length > 500) throw new ArgumentException("Maximum alt text length is 500.", nameof(altText));

        Id = id;
        CourtSubmissionId = courtSubmissionId;
        ImageUrl = uri.AbsoluteUri;
        AltText = alt;
        DisplayOrder = displayOrder;
        IsPrimary = isPrimary;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid CourtSubmissionId { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public string? AltText { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
