using Kitchain.Application.Courts;
using Kitchain.Domain.Courts;
using Kitchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Infrastructure.Courts;

public sealed class EfCourtDiscoveryReader(KitchainDbContext db) : ICourtDiscoveryReader
{
    public async Task<CourtPage> SearchAsync(CourtSearch search, CancellationToken cancellationToken)
    {
        var query = db.Courts.AsNoTracking().Where(c => c.Status == CourtStatus.Published);
        if (search.City is not null)
        {
            var city = search.City.ToUpperInvariant();
            query = query.Where(c => c.City.ToUpper() == city);
        }
        if (search.IndoorOutdoor.HasValue) query = query.Where(c => c.IndoorOutdoor == search.IndoorOutdoor);
        if (search.MinCourts.HasValue) query = query.Where(c => c.NumberOfCourts >= search.MinCourts);
        if (search.MaxStartingPrice.HasValue) query = query.Where(c => c.StartingPrice <= search.MaxStartingPrice);
        if (search.CurrencyCode is not null) query = query.Where(c => c.CurrencyCode == search.CurrencyCode);
        if (search.Amenity.HasValue) query = query.Where(c => c.Amenities.Any(a => a.AmenityCode == search.Amenity));

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Skip((search.Page - 1) * search.PageSize).Take(search.PageSize)
            .AsSingleQuery()
            .Select(c => new CourtSummary(c.Id, c.Name, c.City, c.Address, c.NumberOfCourts,
                c.IndoorOutdoor, c.StartingPrice, c.CurrencyCode, c.PriceUnit,
                c.Amenities.OrderBy(a => a.AmenityCode).Select(a => a.AmenityCode).ToList(),
                c.BookingMethod, c.DataSource))
            .ToListAsync(cancellationToken);
        return new CourtPage(items, search.Page, search.PageSize, count);
    }

    public Task<CourtDetail?> FindPublishedAsync(Guid id, CancellationToken cancellationToken) =>
        db.Courts.AsNoTracking().AsSingleQuery()
            .Where(c => c.Id == id && c.Status == CourtStatus.Published)
            .Select(c => new CourtDetail(c.Id, c.Name, c.Address, c.City, c.Region,
                c.Latitude, c.Longitude, c.NumberOfCourts, c.IndoorOutdoor, c.Surface,
                c.OpeningHours, c.StartingPrice, c.CurrencyCode, c.PriceUnit,
                c.Amenities.OrderBy(a => a.AmenityCode).Select(a => a.AmenityCode).ToList(),
                c.Phone, c.WebsiteUrl, c.SocialUrl, c.BookingUrl, c.BookingMethod,
                c.DataSource, c.CreatedAt, c.UpdatedAt,
                c.Photos.OrderByDescending(p => p.IsPrimary).ThenBy(p => p.DisplayOrder).ThenBy(p => p.Id)
                    .Select(p => new CourtPhotoDetail(p.Id, p.ImageUrl, p.AltText, p.DisplayOrder, p.IsPrimary)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);
}
