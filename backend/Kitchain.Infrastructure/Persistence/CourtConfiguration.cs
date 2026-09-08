using Kitchain.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitchain.Infrastructure.Persistence;

internal sealed class CourtConfiguration : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> court)
    {
        court.ToTable("Courts", table =>
        {
            table.HasCheckConstraint("CK_Courts_RequiredText", "length(trim(\"Name\")) > 0 AND length(trim(\"City\")) > 0 AND length(trim(\"Address\")) > 0");
            table.HasCheckConstraint("CK_Courts_NumberOfCourts", "\"NumberOfCourts\" > 0");
            table.HasCheckConstraint("CK_Courts_Coordinates", "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180)");
            table.HasCheckConstraint("CK_Courts_Price", "\"StartingPrice\" IS NULL OR (\"StartingPrice\" >= 0 AND \"CurrencyCode\" IS NOT NULL)");
            table.HasCheckConstraint("CK_Courts_Currency", "\"CurrencyCode\" IS NULL OR \"CurrencyCode\" ~ '^[A-Z]{3}$'");
            table.HasCheckConstraint("CK_Courts_PriceUnit", "\"PriceUnit\" IS NULL OR (\"StartingPrice\" IS NOT NULL AND \"PriceUnit\" IN ('PerHour', 'PerPerson', 'PerSession'))");
            table.HasCheckConstraint("CK_Courts_Status", "\"Status\" IN ('Draft', 'Published', 'Inactive')");
            table.HasCheckConstraint("CK_Courts_IndoorOutdoor", "\"IndoorOutdoor\" IN ('Indoor', 'Outdoor', 'Mixed')");
            table.HasCheckConstraint("CK_Courts_BookingMethod", "\"BookingMethod\" IN ('ExternalPlatform', 'Website', 'GoogleForm', 'Phone', 'Message', 'WalkIn', 'Other')");
            table.HasCheckConstraint("CK_Courts_DataSource", "\"DataSource\" IN ('OwnerSupplied', 'CommunitySupplied', 'KitchainCurated')");
            table.HasCheckConstraint("CK_Courts_Timestamps", "\"UpdatedAt\" >= \"CreatedAt\"");
        });
        court.HasKey(c => c.Id);
        court.Property(c => c.Id).ValueGeneratedNever();
        court.Property(c => c.Name).HasMaxLength(200).IsRequired();
        court.Property(c => c.Address).HasMaxLength(500).IsRequired();
        court.Property(c => c.City).HasMaxLength(100).IsRequired();
        court.Property(c => c.Region).HasMaxLength(100);
        court.Property(c => c.Latitude).HasPrecision(9, 6);
        court.Property(c => c.Longitude).HasPrecision(9, 6);
        court.Property(c => c.Surface).HasMaxLength(100);
        court.Property(c => c.OpeningHours).HasMaxLength(1000);
        court.Property(c => c.StartingPrice).HasPrecision(12, 2);
        court.Property(c => c.CurrencyCode).HasMaxLength(3);
        court.Property(c => c.Phone).HasMaxLength(50);
        court.Property(c => c.WebsiteUrl).HasMaxLength(2048);
        court.Property(c => c.SocialUrl).HasMaxLength(2048);
        court.Property(c => c.BookingUrl).HasMaxLength(2048);
        court.Property(c => c.IndoorOutdoor).HasConversion<string>().HasMaxLength(20);
        court.Property(c => c.BookingMethod).HasConversion<string>().HasMaxLength(30);
        court.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        court.Property(c => c.DataSource).HasConversion<string>().HasMaxLength(30);
        court.Property(c => c.PriceUnit).HasConversion<string>().HasMaxLength(20);
        court.HasIndex(c => new { c.Status, c.Name, c.Id });
        court.HasMany(c => c.Amenities).WithOne().HasForeignKey(a => a.CourtId).OnDelete(DeleteBehavior.Cascade);
        court.Navigation(c => c.Amenities).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    public void Configure(EntityTypeBuilder<Amenity> amenity)
    {
        amenity.ToTable("Amenities");
        amenity.HasKey(a => a.Code);
        amenity.Property(a => a.Code).HasConversion<string>().HasMaxLength(40).ValueGeneratedNever();
        amenity.HasData(Enum.GetValues<AmenityCode>().Select(code => new Amenity(code)));
    }
}

internal sealed class CourtAmenityConfiguration : IEntityTypeConfiguration<CourtAmenity>
{
    public void Configure(EntityTypeBuilder<CourtAmenity> amenity)
    {
        amenity.ToTable("CourtAmenities");
        amenity.HasKey(a => new { a.CourtId, a.AmenityCode });
        amenity.Property(a => a.AmenityCode).HasConversion<string>().HasMaxLength(40);
        amenity.HasOne<Amenity>().WithMany().HasForeignKey(a => a.AmenityCode).OnDelete(DeleteBehavior.Restrict);
    }
}
