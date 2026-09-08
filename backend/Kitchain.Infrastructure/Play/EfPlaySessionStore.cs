using System.Data;
using System.Security.Cryptography;
using Kitchain.Application.Play;
using Kitchain.Domain.Play;
using Kitchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kitchain.Infrastructure.Play;

public sealed class PlayJoinCodeGenerator : IPlayJoinCodeGenerator
{
    public string Generate() => new(Enumerable.Range(0, PlaySession.JoinCodeLength)
        .Select(_ => PlaySession.JoinCodeAlphabet[RandomNumberGenerator.GetInt32(PlaySession.JoinCodeAlphabet.Length)])
        .ToArray());
}

public sealed class EfPlaySessionStore(KitchainDbContext db) : IPlaySessionStore
{
    public async Task<bool> TryAddAsync(PlaySession session, CancellationToken cancellationToken)
    {
        db.Set<PlaySession>().Add(session);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_PlaySessions_JoinCode" })
        {
            db.Entry(session).State = EntityState.Detached;
            return false;
        }
    }

    public Task<PlaySession?> FindAsync(string code, CancellationToken cancellationToken) =>
        db.Set<PlaySession>().AsNoTracking().AsSingleQuery().Include(s => s.Players)
            .SingleOrDefaultAsync(s => s.JoinCode == code, cancellationToken);

    public async Task<PlaySession?> UpdateAsync(string code, Action<PlaySession> update, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        // Serialize all session mutations before checking names, capacity or allocating queue tickets.
        var rows = await db.Set<PlaySession>()
            .FromSqlInterpolated($"SELECT * FROM \"PlaySessions\" WHERE \"JoinCode\" = {code} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var session = rows.SingleOrDefault();
        if (session is null) return null;
        await db.Entry(session).Collection(s => s.Players).LoadAsync(cancellationToken);
        update(session);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }
}
