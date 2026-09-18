using System.Linq.Expressions;
using Kitchain.Domain.Gear;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitchain.Infrastructure.Persistence.Gear;

internal static class GearSpecificationMapping
{
    internal static void Configure<T>(EntityTypeBuilder<T> b, Expression<Func<T, GearSpecifications?>> property, string table) where T : class
    {
        string[] numbers = ["ThicknessMm", "AdvertisedWeightMinOz", "AdvertisedWeightMaxOz", "LengthInches", "WidthInches",
            "HandleLengthInches", "GripCircumferenceInches", "SwingWeightKgCm2", "TwistWeightKgCm2", "MeasuredWeightOz"];
        b.OwnsOne(property, s =>
        {
            foreach (var name in numbers) s.Property<decimal?>(name).HasColumnName(name).HasPrecision(10, 3);
            s.Property(x => x.FaceMaterial).HasColumnName("FaceMaterial").HasMaxLength(160);
            s.Property(x => x.CoreMaterial).HasColumnName("CoreMaterial").HasMaxLength(160);
            s.Property(x => x.Construction).HasColumnName("Construction").HasMaxLength(240);
            s.Property(x => x.Shape).HasColumnName("Shape").HasConversion<string>().HasMaxLength(20);
        });
        b.Navigation(property).IsRequired();
        b.ToTable(t =>
        {
            t.HasCheckConstraint($"CK_{table}_Positive", string.Join(" AND ", numbers.Select(n => $"(\"{n}\" IS NULL OR \"{n}\" > 0)")));
            t.HasCheckConstraint($"CK_{table}_Ranges", "(\"AdvertisedWeightMinOz\" IS NULL OR \"AdvertisedWeightMaxOz\" IS NULL OR \"AdvertisedWeightMinOz\" <= \"AdvertisedWeightMaxOz\") AND (\"HandleLengthInches\" IS NULL OR \"LengthInches\" IS NULL OR \"HandleLengthInches\" <= \"LengthInches\")");
            t.HasCheckConstraint($"CK_{table}_Shape", "\"Shape\" IS NULL OR \"Shape\" IN ('Standard','Widebody','Hybrid','Elongated','Other')");
        });
    }
}
internal sealed class GearDataSourceConfiguration : IEntityTypeConfiguration<GearDataSource>
{
    public void Configure(EntityTypeBuilder<GearDataSource> b)
    {
        GearMapping.Entity(b, "GearDataSources"); GearMapping.Text(b, "Name", 200);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Url).HasMaxLength(2048); b.Property(x => x.PublisherDomain).HasMaxLength(253);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_GearDataSources_Type", "\"Type\" IN ('Manufacturer','Retailer','Marketplace','IndependentTest','Editorial','Manual')");
            t.HasCheckConstraint("CK_GearDataSources_Required", "length(trim(\"Name\")) > 0 AND (\"Type\" = 'Manual' OR \"Url\" IS NOT NULL)");
            t.HasCheckConstraint("CK_GearDataSources_Time", "\"LastCheckedAt\" >= \"RetrievedAt\" AND \"CreatedAt\" >= \"RetrievedAt\"");
        });
        b.HasIndex(x => new { x.Name, x.Id });
    }
}
internal sealed class PaddleSpecificationEvidenceConfiguration : IEntityTypeConfiguration<PaddleSpecificationEvidence>
{
    public void Configure(EntityTypeBuilder<PaddleSpecificationEvidence> b)
    {
        GearMapping.Entity(b, "GearSpecificationEvidence");
        GearSpecificationMapping.Configure(b, x => x.Specifications, "GearSpecificationEvidence");
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_GearSpecificationEvidence_Type", "\"Type\" IN ('ManufacturerStated','RetailerStated','IndependentlyMeasured','KitchainVerified')");
            t.HasCheckConstraint("CK_GearSpecificationEvidence_Time", "\"CreatedAt\" >= \"ObservedAt\"");
        });
        b.HasOne<PaddleVariant>().WithMany().HasForeignKey(x => x.PaddleVariantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GearDataSource>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PaddleVariantId, x.ObservedAt, x.Id });
    }
}
internal sealed class GearImportCandidateConfiguration : IEntityTypeConfiguration<GearImportCandidate>
{
    public void Configure(EntityTypeBuilder<GearImportCandidate> b)
    {
        GearMapping.Entity(b, "GearImportCandidates");
        GearSpecificationMapping.Configure(b, x => x.Specifications, "GearImportCandidates");
        foreach (var name in new[] { "BrandName", "VariantName" }) GearMapping.Text(b, name, 120);
        GearMapping.Text(b, "PaddleName", 200);
        foreach (var name in new[] { "BrandSlug", "PaddleSlug", "VariantSlug" }) GearMapping.Text(b, name, 160);
        b.Property(x => x.ManufacturerSku).HasMaxLength(100); b.Property(x => x.ReviewNotes).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.EvidenceType).HasConversion<string>().HasMaxLength(30);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_GearImportCandidates_Evidence", "\"EvidenceType\" IN ('ManufacturerStated','RetailerStated','IndependentlyMeasured','KitchainVerified')");
            t.HasCheckConstraint("CK_GearImportCandidates_Decision", "(\"Status\" = 'Pending' AND \"ReviewedAt\" IS NULL AND \"MatchedVariantId\" IS NULL) OR (\"Status\" = 'Rejected' AND \"ReviewedAt\" IS NOT NULL AND \"MatchedVariantId\" IS NULL) OR (\"Status\" = 'Approved' AND \"ReviewedAt\" IS NOT NULL AND \"MatchedVariantId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_GearImportCandidates_Time", "\"CreatedAt\" >= \"ObservedAt\" AND (\"ReviewedAt\" IS NULL OR \"ReviewedAt\" >= \"CreatedAt\")");
        });
        b.HasOne<GearDataSource>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PaddleVariant>().WithMany().HasForeignKey(x => x.MatchedVariantId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Status, x.CreatedAt, x.Id });
    }
}
