using System.Data;
using Kitchain.Application.Gear;
using Kitchain.Domain.Gear;
using Kitchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kitchain.Infrastructure.Gear;

public sealed class EfGearImportStore(KitchainDbContext db) : IGearImportStore
{
    public async Task AddSourceAsync(GearDataSource source, CancellationToken ct)
    { db.Add(source); await db.SaveChangesAsync(ct); }
    public Task<GearDataSource?> FindSourceAsync(Guid id, CancellationToken ct) =>
        db.Set<GearDataSource>().AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);
    public async Task<IReadOnlyList<GearDataSource>> SourcesAsync(int offset, CancellationToken ct) =>
        await db.Set<GearDataSource>().AsNoTracking().OrderBy(s => s.Name).ThenBy(s => s.Id).Skip(offset).Take(50).ToListAsync(ct);
    public async Task AddCandidateAsync(GearImportCandidate candidate, CancellationToken ct)
    { db.Add(candidate); await db.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<GearImportCandidate>> ListAsync(GearImportStatus status, int offset, CancellationToken ct) =>
        await db.Set<GearImportCandidate>().AsNoTracking().Where(c => c.Status == status)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).Skip(offset).Take(50).ToListAsync(ct);
    public Task<GearImportCandidate?> FindAsync(Guid id, CancellationToken ct) =>
        db.Set<GearImportCandidate>().AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
    public async Task<GearImportCandidate> ReviewAsync(Guid id,
        Func<GearImportCandidate, CancellationToken, Task> review, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var rows = await db.Set<GearImportCandidate>().FromSqlInterpolated(
            $"SELECT * FROM \"GearImportCandidates\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct);
        var candidate = rows.SingleOrDefault() ?? throw new KeyNotFoundException("Import candidate not found.");
        await review(candidate, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new GearConflictException("Catalog identity already exists or changed during review. Inspect it and explicitly select the target variant to add evidence.");
        }
        return candidate;
    }
    public async Task<GearBrand?> MatchBrandAsync(string slug, string normalizedName, CancellationToken ct)
    {
        var brands = await db.Set<GearBrand>().Where(b => b.Slug == slug || b.NormalizedName == normalizedName).Take(2).ToListAsync(ct);
        if (brands.Count > 1) throw new GearConflictException("Brand name and slug match different brands.");
        return brands.SingleOrDefault();
    }
    public Task<Paddle?> MatchPaddleAsync(Guid brandId, string slug, CancellationToken ct) =>
        db.Set<Paddle>().SingleOrDefaultAsync(p => p.BrandId == brandId && p.Slug == slug, ct);
    public Task<Paddle?> FindPaddleAsync(Guid id, CancellationToken ct) => db.Set<Paddle>().SingleOrDefaultAsync(p => p.Id == id, ct);
    public Task<PaddleVariant?> FindVariantAsync(Guid id, CancellationToken ct) => db.Set<PaddleVariant>().SingleOrDefaultAsync(v => v.Id == id, ct);
    public void Stage(GearPublication publication)
    {
        if (publication.Brand is not null) db.Add(publication.Brand);
        if (publication.Paddle is not null) db.Add(publication.Paddle);
        if (publication.Variant is not null) db.Add(publication.Variant);
        db.Add(publication.Evidence);
    }
}
