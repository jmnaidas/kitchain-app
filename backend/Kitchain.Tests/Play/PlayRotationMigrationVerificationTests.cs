using Kitchain.Domain.Play;
using Kitchain.Infrastructure.Persistence.Migrations;
using Kitchain.Tests.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace Kitchain.Tests.Play;

// This class owns its fixture/schema; rollback/upgrade never touches another fixture or app tables.
public sealed class PlayRotationMigrationVerificationTests(PostgresCourtFixture fixture) : IClassFixture<PostgresCourtFixture>
{
    [Fact]
    public void Migration_only_replaces_the_rotation_constraint()
    {
        var operations = new AddPlayRotationPresets().UpOperations;
        Assert.Equal(2, operations.Count);
        var drop = Assert.IsType<DropCheckConstraintOperation>(operations[0]);
        var add = Assert.IsType<AddCheckConstraintOperation>(operations[1]);
        Assert.Equal("PlaySessions", drop.Table);
        Assert.Equal(drop.Name, add.Name);
        Assert.Equal(drop.Table, add.Table);
        Assert.All(Enum.GetNames<PlayRotationMode>(), name => Assert.Contains($"'{name}'", add.Sql));
    }

    [PostgresFact]
    public async Task Upgrade_preserves_existing_fair_rows_and_database_accepts_only_supported_presets()
    {
        await using var db = fixture.CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260915120000_AddPlayRosterHistory");
        var session = new PlaySession(Guid.NewGuid(), "ABCDEF", "Existing session", new(2026, 9, 16),
            new(18, 0), new(21, 0), 1, null, new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero));
        db.Add(session);
        await db.SaveChangesAsync();
        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        var preserved = await db.Set<PlaySession>().SingleAsync(s => s.Id == session.Id);
        Assert.Equal(PlayRotationMode.FairRotation, preserved.RotationMode);
        Assert.Equal(session.Name, preserved.Name);
        Assert.Equal(session.CreatedAt, preserved.CreatedAt);
        foreach (var mode in Enum.GetValues<PlayRotationMode>())
        {
            var stored = mode.ToString();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PlaySessions\" SET \"RotationMode\" = {stored} WHERE \"Id\" = {session.Id}");
            db.ChangeTracker.Clear();
            Assert.Equal(mode, (await db.Set<PlaySession>().SingleAsync(s => s.Id == session.Id)).RotationMode);
        }
        var invalid = "Unknown";
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"PlaySessions\" SET \"RotationMode\" = {invalid} WHERE \"Id\" = {session.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
