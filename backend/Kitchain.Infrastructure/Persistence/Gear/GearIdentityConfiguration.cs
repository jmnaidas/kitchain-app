using Kitchain.Domain.Gear;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitchain.Infrastructure.Persistence.Gear;

internal static class GearMapping
{
    internal static void Entity<T>(EntityTypeBuilder<T> b, string table) where T : class
    { b.ToTable(table); b.HasKey("Id"); b.Property<Guid>("Id").ValueGeneratedNever(); }
    internal static void Text<T>(EntityTypeBuilder<T> b, string field, int max) where T : class =>
        b.Property<string>(field).HasMaxLength(max).IsRequired();
    internal static void IdentityChecks<T>(EntityTypeBuilder<T> b, string table) where T : class =>
        b.ToTable(t =>
        {
            t.HasCheckConstraint($"CK_{table}_Identity", "length(trim(\"Name\")) > 0 AND \"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
            t.HasCheckConstraint($"CK_{table}_Time", "\"UpdatedAt\" >= \"CreatedAt\"");
        });
    internal static void Publication<T>(EntityTypeBuilder<T> b, string table) where T : class
    {
        b.Property<GearPublicationStatus>("Status").HasConversion<string>().HasMaxLength(20);
        b.ToTable(t => t.HasCheckConstraint($"CK_{table}_Status", "\"Status\" IN ('Draft','Published','Archived')"));
    }
}

internal sealed class GearBrandConfiguration : IEntityTypeConfiguration<GearBrand>
{
    public void Configure(EntityTypeBuilder<GearBrand> b)
    {
        GearMapping.Entity(b, "GearBrands"); GearMapping.IdentityChecks(b, "GearBrands");
        GearMapping.Text(b, "Name", 120); GearMapping.Text(b, "NormalizedName", 120); GearMapping.Text(b, "Slug", 160);
        b.Property(x => x.WebsiteUrl).HasMaxLength(2048);
        b.HasIndex(x => x.NormalizedName).IsUnique(); b.HasIndex(x => x.Slug).IsUnique();
    }
}
internal sealed class PaddleConfiguration : IEntityTypeConfiguration<Paddle>
{
    public void Configure(EntityTypeBuilder<Paddle> b)
    {
        GearMapping.Entity(b, "GearPaddles"); GearMapping.IdentityChecks(b, "GearPaddles"); GearMapping.Publication(b, "GearPaddles");
        GearMapping.Text(b, "Name", 200); GearMapping.Text(b, "Slug", 160);
        b.Property(x => x.ModelFamily).HasMaxLength(120); b.Property(x => x.Description).HasMaxLength(2000);
        b.ToTable(t => t.HasCheckConstraint("CK_GearPaddles_Year", "\"ReleaseYear\" IS NULL OR \"ReleaseYear\" BETWEEN 1965 AND 2200"));
        b.HasOne<GearBrand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BrandId, x.Slug }).IsUnique(); b.HasIndex(x => new { x.Status, x.Name, x.Id });
    }
}
internal sealed class PaddleVariantConfiguration : IEntityTypeConfiguration<PaddleVariant>
{
    public void Configure(EntityTypeBuilder<PaddleVariant> b)
    {
        GearMapping.Entity(b, "GearPaddleVariants"); GearMapping.IdentityChecks(b, "GearPaddleVariants"); GearMapping.Publication(b, "GearPaddleVariants");
        GearMapping.Text(b, "Name", 120); GearMapping.Text(b, "Slug", 160);
        b.Property(x => x.ManufacturerSku).HasMaxLength(100); b.Property(x => x.NormalizedSku).HasMaxLength(100);
        b.HasOne<Paddle>().WithMany().HasForeignKey(x => x.PaddleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PaddleId, x.Slug }).IsUnique();
        b.HasIndex(x => new { x.PaddleId, x.NormalizedSku }).IsUnique().HasFilter("\"NormalizedSku\" IS NOT NULL");
        b.HasIndex(x => new { x.PaddleId, x.Status });
    }
}
