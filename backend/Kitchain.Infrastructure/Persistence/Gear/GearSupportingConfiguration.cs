using Kitchain.Domain.Gear;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitchain.Infrastructure.Persistence.Gear;

internal sealed class PaddleListingConfiguration : IEntityTypeConfiguration<PaddleListing>
{
    public void Configure(EntityTypeBuilder<PaddleListing> b)
    {
        GearMapping.Entity(b, "GearPaddleListings");
        GearMapping.Text(b, "SellerName", 160); GearMapping.Text(b, "Url", 2048); GearMapping.Text(b, "CurrencyCode", 3);
        b.Property(x => x.Price).HasPrecision(12, 2); b.Property(x => x.OriginalPrice).HasPrecision(12, 2);
        b.HasOne<PaddleVariant>().WithMany().HasForeignKey(x => x.PaddleVariantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GearDataSource>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PaddleVariantId, x.LastCheckedAt, x.Id });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_GearPaddleListings_Price", "(\"Price\" IS NULL OR \"Price\" >= 0) AND (\"OriginalPrice\" IS NULL OR \"OriginalPrice\" >= 0)");
            t.HasCheckConstraint("CK_GearPaddleListings_Currency", "\"CurrencyCode\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("CK_GearPaddleListings_Time", "\"UpdatedAt\" >= \"CreatedAt\" AND \"CreatedAt\" >= \"LastCheckedAt\"");
        });
    }
}

internal sealed class PaddleImageConfiguration : IEntityTypeConfiguration<PaddleImage>
{
    public void Configure(EntityTypeBuilder<PaddleImage> b)
    {
        GearMapping.Entity(b, "GearPaddleImages"); GearMapping.Text(b, "ImageUrl", 2048);
        b.Property(x => x.AltText).HasMaxLength(300);
        b.HasOne<PaddleVariant>().WithMany().HasForeignKey(x => x.PaddleVariantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GearDataSource>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PaddleVariantId).IsUnique().HasFilter("\"IsPrimary\" = TRUE");
        b.HasIndex(x => new { x.PaddleVariantId, x.SortOrder, x.Id });
        b.ToTable(t => t.HasCheckConstraint("CK_GearPaddleImages_Order", "\"SortOrder\" >= 0"));
    }
}

internal sealed class PaddlePerformanceProfileConfiguration : IEntityTypeConfiguration<PaddlePerformanceProfile>
{
    public void Configure(EntityTypeBuilder<PaddlePerformanceProfile> b)
    {
        b.ToTable("GearPerformanceProfiles"); b.HasKey(x => x.PaddleVariantId);
        b.Property(x => x.PaddleVariantId).ValueGeneratedNever();
        GearMapping.Text(b, "MethodVersion", 80); GearMapping.Publication(b, "GearPerformanceProfiles");
        b.Property(x => x.OverallStyle).HasConversion<string>().HasMaxLength(20);
        b.HasOne<PaddleVariant>().WithOne().HasForeignKey<PaddlePerformanceProfile>(x => x.PaddleVariantId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_GearPerformanceProfiles_Ratings", string.Join(" AND ", new[] { "Power", "Control", "Forgiveness", "HandSpeed", "Spin", "SweetSpot" }.Select(n => $"(\"{n}\" IS NULL OR \"{n}\" BETWEEN 1 AND 10)")));
            t.HasCheckConstraint("CK_GearPerformanceProfiles_Method", "length(trim(\"MethodVersion\")) > 0");
            t.HasCheckConstraint("CK_GearPerformanceProfiles_Style", "\"OverallStyle\" IS NULL OR \"OverallStyle\" IN ('Power','Control','Balanced')");
        });
    }
}
