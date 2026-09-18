using Kitchain.Domain.Gear;

namespace Kitchain.Application.Gear;

public sealed record GearBrandSummary(Guid Id, string Name, string Slug);
public sealed record GearVariantSummary(Guid Id, string Name, string Slug, string? PrimaryImageUrl);
public sealed record GearPaddleSummary(Guid Id, GearBrandSummary Brand, string Name, string Slug,
    IReadOnlyList<GearVariantSummary> Variants);
public sealed record GearEvidenceDetail(Guid Id, GearSourceDetail Source, GearEvidenceType Type,
    DateTimeOffset ObservedAt, GearSpecificationInput Specifications);
public sealed record GearImageDetail(Guid Id, string ImageUrl, string? AltText, int SortOrder, bool IsPrimary, GearSourceDetail? Source);
public sealed record GearListingDetail(Guid Id, GearSourceDetail Source, string SellerName, string Url,
    string CurrencyCode, decimal? Price, decimal? OriginalPrice, bool? InStock, DateTimeOffset LastCheckedAt);
public sealed record GearPerformanceDetail(string MethodVersion, int? Power, int? Control, int? Forgiveness,
    int? HandSpeed, int? Spin, int? SweetSpot, PaddleStyle? OverallStyle, DateTimeOffset UpdatedAt);
public sealed record GearVariantDetail(Guid Id, string Name, string Slug, string? ManufacturerSku,
    IReadOnlyList<GearEvidenceDetail> Evidence, IReadOnlyList<GearImageDetail> Images,
    IReadOnlyList<GearListingDetail> Listings, GearPerformanceDetail? KitchainPerformance);
public sealed record GearPaddleDetail(Guid Id, GearBrandSummary Brand, string Name, string Slug,
    string? ModelFamily, string? Description, int? ReleaseYear, IReadOnlyList<GearVariantDetail> Variants);

public interface IGearCatalogReader
{
    // Fixed 25-product pages, stable name/ID ordering; no speculative search/filter API in G1.
    Task<IReadOnlyList<GearPaddleSummary>> ListAsync(int offset, CancellationToken ct);
    Task<GearPaddleDetail?> FindAsync(Guid id, CancellationToken ct);
}
