using Kitchain.Domain.Courts;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Infrastructure.Persistence;

/// <summary>Explicit opt-in samples. Never part of migrations or normal application startup.</summary>
public static class DevelopmentCourtSeeder
{
    public static async Task<int> SeedAsync(KitchainDbContext db, CancellationToken cancellationToken = default)
    {
        var samples = CreateSamples();
        var sampleIds = samples.Select(c => c.Id).ToArray();
        var existing = await db.Courts.Where(c => sampleIds.Contains(c.Id)).Select(c => c.Id)
            .ToListAsync(cancellationToken);
        var missing = samples.Where(c => !existing.Contains(c.Id)).ToArray();
        db.Courts.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        return missing.Length;
    }

    private static Court[] CreateSamples()
    {
        var timestamp = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        Court Sample(int number, string name, string city, int count, IndoorOutdoorType type,
            decimal? price, BookingMethod booking, CourtStatus status, AmenityCode[] amenities,
            CourtDataSource source = CourtDataSource.KitchainCurated) => new(
                Guid.Parse($"00000000-0000-4000-8000-{number:D12}"),
                $"Demo — {name} (fictional)", $"{number} Fictional Lane — development sample only", city,
                count, type, booking, source, status, timestamp, amenities,
                region: "Metro Manila", surface: "Acrylic (sample)",
                openingHours: "Sample only: 06:00–22:00. Not an actual business schedule.",
                startingPrice: price, currencyCode: price.HasValue ? "PHP" : null,
                priceUnit: price.HasValue ? PriceUnit.PerHour : null,
                websiteUrl: "https://example.com/fictional-kitchain-venues",
                bookingUrl: booking is BookingMethod.ExternalPlatform or BookingMethod.Website or BookingMethod.GoogleForm
                    ? $"https://example.com/fictional-booking/{number}" : null);

        return
        [
            Sample(1, "Kitchain Makati Club", "Makati", 6, IndoorOutdoorType.Indoor, 600,
                BookingMethod.ExternalPlatform, CourtStatus.Published,
                [AmenityCode.Parking, AmenityCode.Restroom, AmenityCode.AirConditioning, AmenityCode.PaddleRental]),
            Sample(2, "North Court QC", "Quezon City", 4, IndoorOutdoorType.Outdoor, 300,
                BookingMethod.WalkIn, CourtStatus.Published,
                [AmenityCode.Restroom, AmenityCode.SeatingOrWaitingArea], CourtDataSource.CommunitySupplied),
            Sample(3, "Southside Pickleball Hub", "Muntinlupa", 8, IndoorOutdoorType.Mixed, 450,
                BookingMethod.Website, CourtStatus.Published,
                [AmenityCode.Parking, AmenityCode.Shower, AmenityCode.ChangingRoom, AmenityCode.Coaching], CourtDataSource.OwnerSupplied),
            Sample(4, "Pasig Rally Hall", "Pasig", 3, IndoorOutdoorType.Indoor, 800,
                BookingMethod.GoogleForm, CourtStatus.Published,
                [AmenityCode.AirConditioning, AmenityCode.Lockers, AmenityCode.ProShop]),
            Sample(5, "Makati Garden Courts", "Makati", 2, IndoorOutdoorType.Outdoor, null,
                BookingMethod.Message, CourtStatus.Published,
                [AmenityCode.FoodAndDrinks, AmenityCode.BallOrEquipmentRental]),
            Sample(6, "Pending Manila Club", "Manila", 5, IndoorOutdoorType.Mixed, 200,
                BookingMethod.Phone, CourtStatus.Draft, [AmenityCode.Parking]),
            Sample(7, "Retired Taguig Courts", "Taguig", 4, IndoorOutdoorType.Outdoor, 250,
                BookingMethod.Other, CourtStatus.Inactive, [AmenityCode.Other])
        ];
    }
}
