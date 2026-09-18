using Kitchain.Domain.Gear;
using Kitchain.Infrastructure.Persistence;
using Kitchain.Infrastructure.Persistence.Migrations;
using Kitchain.Tests.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace Kitchain.Tests.Gear;

public sealed class GearPersistenceTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    [Fact]
    public void Migration_only_creates_Gear_tables_and_indexes_without_seed_data()
    {
        var migration = new AddGearFoundation();
        var tables = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(9, tables.Length);
        Assert.All(tables, t => Assert.StartsWith("Gear", t.Name));
        Assert.All(migration.UpOperations, op => Assert.True(op is CreateTableOperation or CreateIndexOperation));
        Assert.All(migration.DownOperations, op => Assert.StartsWith("Gear", Assert.IsType<DropTableOperation>(op).Name));
    }

    [PostgresFact]
    public async Task Local_migration_roundtrip_preserves_existing_module_tables_and_has_no_model_drift()
    {
        // A separate fixture owns this empty Gear schema; no shared test data or developer schema is rolled back.
        await using var isolated = new FixtureLifetime();
        await isolated.Fixture.InitializeAsync();
        await using var db = isolated.Fixture.CreateDbContext();
        var before = await TableCounts(db);
        var migrations = db.Database.GetMigrations().ToArray();
        Assert.EndsWith("_AddGearFoundation", migrations[^1]);
        await db.GetService<IMigrator>().MigrateAsync(migrations[^2]);
        Assert.Equal(before, await TableCounts(db));
        await db.Database.MigrateAsync();
        Assert.Equal(before, await TableCounts(db));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Set<GearBrand>().ToListAsync());
    }

    private static async Task<string[]> TableCounts(KitchainDbContext db)
    {
        var names = await db.Database.SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = current_schema() AND tablename NOT LIKE 'Gear%' AND tablename <> '__EFMigrationsHistory' ORDER BY tablename").ToListAsync();
        var counts = new List<string>();
        foreach (var name in names)
        {
            var quoted = name.Replace("\"", "\"\"");
            // Identifiers cannot be SQL parameters. These come from this disposable schema's
            // pg_tables rows and are double-quote escaped before interpolation.
#pragma warning disable EF1002
            var count = await db.Database.SqlQueryRaw<long>($"SELECT count(*) AS \"Value\" FROM \"{quoted}\"").SingleAsync();
#pragma warning restore EF1002
            counts.Add($"{name}:{count}");
        }
        return counts.ToArray();
    }

    [PostgresTheory]
    [InlineData("brandSlug", PostgresErrorCodes.UniqueViolation)]
    [InlineData("brandName", PostgresErrorCodes.UniqueViolation)]
    [InlineData("paddleSlug", PostgresErrorCodes.UniqueViolation)]
    [InlineData("variantSlug", PostgresErrorCodes.UniqueViolation)]
    [InlineData("variantSku", PostgresErrorCodes.UniqueViolation)]
    [InlineData("primaryImage", PostgresErrorCodes.UniqueViolation)]
    [InlineData("sourceFk", PostgresErrorCodes.ForeignKeyViolation)]
    [InlineData("brandFk", PostgresErrorCodes.ForeignKeyViolation)]
    [InlineData("price", PostgresErrorCodes.CheckViolation)]
    [InlineData("currency", PostgresErrorCodes.CheckViolation)]
    [InlineData("performance", PostgresErrorCodes.CheckViolation)]
    [InlineData("weightRange", PostgresErrorCodes.CheckViolation)]
    [InlineData("negativeSpec", PostgresErrorCodes.CheckViolation)]
    [InlineData("decision", PostgresErrorCodes.CheckViolation)]
    public async Task PostgreSQL_enforces_important_identity_provenance_and_value_constraints(string scenario, string expected)
    {
        await using var db = fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        var brand = new GearBrand(Guid.NewGuid(), "Synthetic " + suffix, "synthetic-" + suffix, now);
        var paddle = new Paddle(Guid.NewGuid(), brand.Id, "Synthetic", "synthetic", now);
        var variant = new PaddleVariant(Guid.NewGuid(), paddle.Id, "16", "16", now, "LAB-16");
        var source = new GearDataSource(Guid.NewGuid(), GearSourceType.Manual, "Synthetic test", null, now, now);
        var listing = new PaddleListing(Guid.NewGuid(), variant.Id, source.Id, "Fictional", "https://example.test", "USD", 1, null, null, now, now);
        var profile = new PaddlePerformanceProfile(variant.Id, "synthetic-v1", now, control: 5);
        var evidence = new PaddleSpecificationEvidence(Guid.NewGuid(), variant.Id, source.Id, GearEvidenceType.ManufacturerStated, new(thicknessMm: 16), now, now);
        var candidate = new GearImportCandidate(Guid.NewGuid(), source.Id, brand.Name, brand.Slug, paddle.Name, paddle.Slug,
            variant.Name, variant.Slug, null, GearEvidenceType.ManufacturerStated, new(), now, now);
        db.AddRange(brand, paddle, variant, source, listing, profile, evidence, candidate,
            new PaddleImage(Guid.NewGuid(), variant.Id, "https://example.test/a.png", 0, true, now));
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            switch (scenario)
            {
                case "brandSlug": db.Add(new GearBrand(Guid.NewGuid(), "Other " + suffix, brand.Slug, now)); break;
                case "brandName": db.Add(new GearBrand(Guid.NewGuid(), brand.Name.ToUpperInvariant(), "other-" + suffix, now)); break;
                case "paddleSlug": db.Add(new Paddle(Guid.NewGuid(), brand.Id, "Other", paddle.Slug, now)); break;
                case "variantSlug": db.Add(new PaddleVariant(Guid.NewGuid(), paddle.Id, "Other", variant.Slug, now)); break;
                case "variantSku": db.Add(new PaddleVariant(Guid.NewGuid(), paddle.Id, "Other", "other", now, "lab-16")); break;
                case "primaryImage": db.Add(new PaddleImage(Guid.NewGuid(), variant.Id, "https://example.test/b.png", 1, true, now)); break;
                case "sourceFk": db.Add(new PaddleSpecificationEvidence(Guid.NewGuid(), variant.Id, Guid.NewGuid(), GearEvidenceType.KitchainVerified, new(), now, now)); break;
                case "brandFk": db.Add(new Paddle(Guid.NewGuid(), Guid.NewGuid(), "Orphan", "orphan", now)); break;
                case "price": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"GearPaddleListings\" SET \"Price\" = -1 WHERE \"Id\" = {listing.Id}"); break;
                case "currency": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"GearPaddleListings\" SET \"CurrencyCode\" = 'xx' WHERE \"Id\" = {listing.Id}"); break;
                case "performance": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"GearPerformanceProfiles\" SET \"Control\" = 11 WHERE \"PaddleVariantId\" = {variant.Id}"); break;
                case "weightRange": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"GearSpecificationEvidence\" SET \"AdvertisedWeightMinOz\" = 9, \"AdvertisedWeightMaxOz\" = 8 WHERE \"Id\" = {evidence.Id}"); break;
                case "negativeSpec": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"GearSpecificationEvidence\" SET \"ThicknessMm\" = -1 WHERE \"Id\" = {evidence.Id}"); break;
                case "decision": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"GearImportCandidates\" SET \"Status\" = 'Approved' WHERE \"Id\" = {candidate.Id}"); break;
                default: throw new InvalidOperationException("Unknown scenario");
            }
            await db.SaveChangesAsync();
        });
        var postgres = Assert.IsType<PostgresException>(error is DbUpdateException ? error.InnerException : error);
        Assert.Equal(expected, postgres.SqlState);
    }

    private sealed class FixtureLifetime : IAsyncDisposable
    {
        public PostgresCourtFixture Fixture { get; } = new();
        public ValueTask DisposeAsync() => new(Fixture.DisposeAsync());
    }
}
