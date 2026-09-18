using Kitchain.Application.Gear;
using Kitchain.Domain.Gear;
using Kitchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Infrastructure.Gear;

public sealed class EfGearCatalogReader(KitchainDbContext db) : IGearCatalogReader
{
    private IQueryable<Paddle> Published => db.Set<Paddle>().AsNoTracking().Where(p =>
        p.Status == GearPublicationStatus.Published && db.Set<PaddleVariant>().Any(v =>
            v.PaddleId == p.Id && v.Status == GearPublicationStatus.Published));

    public async Task<IReadOnlyList<GearPaddleSummary>> ListAsync(int offset, CancellationToken ct)
    {
        if (offset is < 0 or > 100000) throw new ArgumentOutOfRangeException(nameof(offset));
        var products = await (from p in Published
                              join b in db.Set<GearBrand>() on p.BrandId equals b.Id
                              orderby p.Name, p.Id
                              select new { Paddle = p, Brand = b }).Skip(offset).Take(25).ToListAsync(ct);
        var ids = products.Select(x => x.Paddle.Id).ToArray();
        var variants = await db.Set<PaddleVariant>().AsNoTracking()
            .Where(v => ids.Contains(v.PaddleId) && v.Status == GearPublicationStatus.Published)
            .OrderBy(v => v.Name).ThenBy(v => v.Id)
            .Select(v => new
            {
                v.PaddleId,
                Summary = new GearVariantSummary(v.Id, v.Name, v.Slug,
                db.Set<PaddleImage>().Where(i => i.PaddleVariantId == v.Id && i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault())
            })
            .ToListAsync(ct);
        return products.Select(x => new GearPaddleSummary(x.Paddle.Id, Brand(x.Brand), x.Paddle.Name,
            x.Paddle.Slug, variants.Where(v => v.PaddleId == x.Paddle.Id).Select(v => v.Summary).ToArray())).ToArray();
    }

    public async Task<GearPaddleDetail?> FindAsync(Guid id, CancellationToken ct)
    {
        var paddle = await Published.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (paddle is null) return null;
        var brand = await db.Set<GearBrand>().AsNoTracking().SingleAsync(b => b.Id == paddle.BrandId, ct);
        var variants = await db.Set<PaddleVariant>().AsNoTracking().Where(v => v.PaddleId == id && v.Status == GearPublicationStatus.Published)
            .OrderBy(v => v.Name).ThenBy(v => v.Id).ToListAsync(ct);
        var ids = variants.Select(v => v.Id).ToArray();
        var evidence = await db.Set<PaddleSpecificationEvidence>().AsNoTracking().Where(e => ids.Contains(e.PaddleVariantId))
            .OrderByDescending(e => e.ObservedAt).ThenBy(e => e.Id).ToListAsync(ct);
        var images = await db.Set<PaddleImage>().AsNoTracking().Where(i => ids.Contains(i.PaddleVariantId))
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToListAsync(ct);
        var listings = await db.Set<PaddleListing>().AsNoTracking().Where(l => ids.Contains(l.PaddleVariantId))
            .OrderByDescending(l => l.LastCheckedAt).ThenBy(l => l.Id).ToListAsync(ct);
        var profiles = await db.Set<PaddlePerformanceProfile>().AsNoTracking()
            .Where(p => ids.Contains(p.PaddleVariantId) && p.Status == GearPublicationStatus.Published).ToListAsync(ct);
        var sourceIds = evidence.Select(e => e.SourceId).Concat(listings.Select(l => l.SourceId))
            .Concat(images.Where(i => i.SourceId.HasValue).Select(i => i.SourceId!.Value)).Distinct().ToArray();
        var sources = await db.Set<GearDataSource>().AsNoTracking().Where(s => sourceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        return new(paddle.Id, Brand(brand), paddle.Name, paddle.Slug, paddle.ModelFamily, paddle.Description, paddle.ReleaseYear,
            variants.Select(v => new GearVariantDetail(v.Id, v.Name, v.Slug, v.ManufacturerSku,
                evidence.Where(e => e.PaddleVariantId == v.Id).Select(e => new GearEvidenceDetail(e.Id, Source(sources[e.SourceId]),
                    e.Type, e.ObservedAt, GearSpecificationInput.From(e.Specifications))).ToArray(),
                images.Where(i => i.PaddleVariantId == v.Id).Select(i => new GearImageDetail(i.Id, i.ImageUrl, i.AltText, i.SortOrder, i.IsPrimary,
                    i.SourceId.HasValue ? Source(sources[i.SourceId.Value]) : null)).ToArray(),
                listings.Where(l => l.PaddleVariantId == v.Id).Select(l => new GearListingDetail(l.Id, Source(sources[l.SourceId]),
                    l.SellerName, l.Url, l.CurrencyCode, l.Price, l.OriginalPrice, l.InStock, l.LastCheckedAt)).ToArray(),
                Profile(profiles.SingleOrDefault(p => p.PaddleVariantId == v.Id)))).ToArray());
    }
    private static GearBrandSummary Brand(GearBrand b) => new(b.Id, b.Name, b.Slug);
    private static GearSourceDetail Source(GearDataSource s) => new(s.Id, s.Type, s.Name, s.Url, s.PublisherDomain, s.RetrievedAt, s.LastCheckedAt);
    private static GearPerformanceDetail? Profile(PaddlePerformanceProfile? p) => p is null ? null :
        new(p.MethodVersion, p.Power, p.Control, p.Forgiveness, p.HandSpeed, p.Spin, p.SweetSpot, p.OverallStyle, p.UpdatedAt);
}
