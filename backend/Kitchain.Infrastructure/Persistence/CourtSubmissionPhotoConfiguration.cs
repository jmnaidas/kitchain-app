using Kitchain.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitchain.Infrastructure.Persistence;

internal sealed class CourtSubmissionPhotoConfiguration : IEntityTypeConfiguration<CourtSubmissionPhoto>
{
    public void Configure(EntityTypeBuilder<CourtSubmissionPhoto> photo)
    {
        photo.ToTable("CourtSubmissionPhotos", table =>
        {
            table.HasCheckConstraint("CK_CourtSubmissionPhotos_DisplayOrder", "\"DisplayOrder\" >= 0");
            table.HasCheckConstraint("CK_CourtSubmissionPhotos_ImageUrl", "\"ImageUrl\" ~ '^https?://[^[:space:]/?#@]+([/?#][^[:space:]]*)?$'");
        });
        photo.HasKey(p => p.Id);
        photo.Property(p => p.Id).ValueGeneratedNever();
        photo.Property(p => p.ImageUrl).HasMaxLength(2048).IsRequired();
        photo.Property(p => p.AltText).HasMaxLength(500);
        photo.HasIndex(p => p.CourtSubmissionId).IsUnique().HasFilter("\"IsPrimary\"")
            .HasDatabaseName("IX_CourtSubmissionPhotos_OnePrimaryPerCourt");
        photo.HasIndex(p => new { p.CourtSubmissionId, p.IsPrimary, p.DisplayOrder, p.Id })
            .IsDescending(false, true, false, false).HasDatabaseName("IX_CourtSubmissionPhotos_Ordered");
    }
}
