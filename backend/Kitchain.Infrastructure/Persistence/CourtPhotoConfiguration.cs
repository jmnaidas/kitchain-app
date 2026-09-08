using Kitchain.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitchain.Infrastructure.Persistence;

internal sealed class CourtPhotoConfiguration : IEntityTypeConfiguration<CourtPhoto>
{
    public void Configure(EntityTypeBuilder<CourtPhoto> photo)
    {
        photo.ToTable("CourtPhotos", table =>
        {
            table.HasCheckConstraint("CK_CourtPhotos_DisplayOrder", "\"DisplayOrder\" >= 0");
            table.HasCheckConstraint("CK_CourtPhotos_ImageUrl", "\"ImageUrl\" ~ '^https?://[^[:space:]/?#@]+([/?#][^[:space:]]*)?$'");
        });
        photo.HasKey(p => p.Id);
        photo.Property(p => p.Id).ValueGeneratedNever();
        photo.Property(p => p.ImageUrl).HasMaxLength(2048).IsRequired();
        photo.Property(p => p.AltText).HasMaxLength(500);
        photo.HasIndex(p => p.CourtId).IsUnique().HasFilter("\"IsPrimary\"")
            .HasDatabaseName("IX_CourtPhotos_OnePrimaryPerCourt");
        photo.HasIndex(p => new { p.CourtId, p.IsPrimary, p.DisplayOrder, p.Id })
            .IsDescending(false, true, false, false);
    }
}
