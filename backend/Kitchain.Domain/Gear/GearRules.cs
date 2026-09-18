using System.Text.RegularExpressions;

namespace Kitchain.Domain.Gear;

public enum GearPublicationStatus { Draft, Published, Archived }
public enum PaddleShape { Standard, Widebody, Hybrid, Elongated, Other }
public enum GearSourceType { Manufacturer, Retailer, Marketplace, IndependentTest, Editorial, Manual }
public enum GearEvidenceType { ManufacturerStated, RetailerStated, IndependentlyMeasured, KitchainVerified }
public enum PaddleStyle { Power, Control, Balanced }
public enum GearImportStatus { Pending, Approved, Rejected }
public sealed class GearConflictException(string message) : InvalidOperationException(message);

public static class GearRules
{
    public static Guid Id(Guid value) => value != Guid.Empty ? value : throw new ArgumentException("A non-empty ID is required.");
    public static string Text(string? value, int max, string field) =>
        Optional(value, max, field) ?? throw new ArgumentException("A value is required.", field);
    public static string? Optional(string? value, int max, string field)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (text?.Length > max) throw new ArgumentException($"Maximum length is {max}.", field);
        return text;
    }
    public static string Key(string value) => Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();
    public static string Slug(string value)
    {
        var slug = Text(value, 160, "slug").ToLowerInvariant();
        if (!Regex.IsMatch(slug, "^[a-z0-9]+(-[a-z0-9]+)*$"))
            throw new ArgumentException("Use lowercase letters, digits and single hyphens.", "slug");
        return slug;
    }
    public static string? Url(string? value)
    {
        var text = Optional(value, 2048, "url");
        if (text is null) return null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") ||
            string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsoluteUri.Length > 2048)
            throw new ArgumentException("Use an absolute HTTP(S) URL without embedded credentials.", "url");
        return uri.AbsoluteUri;
    }
    public static T EnumValue<T>(T value) where T : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new ArgumentException("Choose a supported value.", typeof(T).Name);
    public static DateTimeOffset Time(DateTimeOffset value) => value != default ? value.ToUniversalTime() :
        throw new ArgumentException("A timestamp is required.");
    public static DateTimeOffset After(DateTimeOffset value, DateTimeOffset earlier) =>
        Time(value) >= earlier ? value.ToUniversalTime() : throw new ArgumentException("Timestamp cannot precede the previous observation.");
    public static decimal? Positive(decimal? value, string field)
    {
        if (value.HasValue && (value <= 0 || value > 9999999.999m || decimal.Round(value.Value, 3) != value))
            throw new ArgumentException("Use a positive value fitting decimal(10,3).", field);
        return value;
    }
    public static decimal? Price(decimal? value)
    {
        if (value.HasValue && (value < 0 || value > 9999999999.99m || decimal.Round(value.Value, 2) != value))
            throw new ArgumentException("Price must be non-negative and fit decimal(12,2).", "price");
        return value;
    }
    public static void Transition(GearPublicationStatus from, GearPublicationStatus to)
    {
        EnumValue(to);
        if (!((from == GearPublicationStatus.Draft && to == GearPublicationStatus.Published) ||
              (from is GearPublicationStatus.Draft or GearPublicationStatus.Published && to == GearPublicationStatus.Archived)))
            throw new GearConflictException("Only Draft publication or Draft/Published archival is supported.");
    }
}
