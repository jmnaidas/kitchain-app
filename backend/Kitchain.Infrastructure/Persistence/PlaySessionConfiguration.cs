using Kitchain.Domain.Play;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitchain.Infrastructure.Persistence;

internal sealed class PlaySessionConfiguration : IEntityTypeConfiguration<PlaySession>
{
    public void Configure(EntityTypeBuilder<PlaySession> session)
    {
        session.ToTable("PlaySessions", table =>
        {
            table.HasCheckConstraint("CK_PlaySessions_JoinCode", "\"JoinCode\" ~ '^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$'");
            table.HasCheckConstraint("CK_PlaySessions_Name", "length(trim(\"Name\")) > 0");
            table.HasCheckConstraint("CK_PlaySessions_Schedule", "\"EndTime\" > \"StartTime\"");
            table.HasCheckConstraint("CK_PlaySessions_Capacity", "\"NumberOfCourts\" > 0 AND (\"MaximumPlayers\" IS NULL OR \"MaximumPlayers\" > 0)");
            table.HasCheckConstraint("CK_PlaySessions_Status", "\"Status\" IN ('Draft', 'Active', 'Ended')");
            table.HasCheckConstraint("CK_PlaySessions_DefaultModes", "\"RotationMode\" = 'FairRotation' AND \"ScoringMode\" = 'Traditional' AND \"GameTo\" = 11 AND \"WinBy\" = 2");
            table.HasCheckConstraint("CK_PlaySessions_QueueCounter", "\"NextQueueOrder\" >= 0");
            table.HasCheckConstraint("CK_PlaySessions_Timestamps", "\"UpdatedAt\" >= \"CreatedAt\"");
        });
        session.HasKey(s => s.Id);
        session.Property(s => s.Id).ValueGeneratedNever();
        session.Property(s => s.JoinCode).HasMaxLength(6).IsRequired();
        session.Property(s => s.Name).HasMaxLength(200).IsRequired();
        session.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        session.Property(s => s.RotationMode).HasConversion<string>().HasMaxLength(30);
        session.Property(s => s.ScoringMode).HasConversion<string>().HasMaxLength(30);
        session.HasIndex(s => s.JoinCode).IsUnique();
        session.Ignore(s => s.WaitingQueue);
        session.HasMany(s => s.Players).WithOne().HasForeignKey(p => p.SessionId).OnDelete(DeleteBehavior.Cascade);
        session.Navigation(s => s.Players).UsePropertyAccessMode(PropertyAccessMode.Field);
        session.HasMany(s => s.Matches).WithOne().HasForeignKey(m => m.SessionId).OnDelete(DeleteBehavior.Cascade);
        session.Navigation(s => s.Matches).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PlaySessionPlayerConfiguration : IEntityTypeConfiguration<PlaySessionPlayer>
{
    public void Configure(EntityTypeBuilder<PlaySessionPlayer> player)
    {
        player.ToTable("PlaySessionPlayers", table =>
        {
            table.HasCheckConstraint("CK_PlaySessionPlayers_Name", "length(trim(\"DisplayName\")) > 0 AND length(trim(\"NormalizedDisplayName\")) > 0");
            table.HasCheckConstraint("CK_PlaySessionPlayers_Identity", "\"IdentityType\" = 'Guest'");
            table.HasCheckConstraint("CK_PlaySessionPlayers_State", "\"State\" IN ('Waiting', 'Playing', 'Resting')");
            table.HasCheckConstraint("CK_PlaySessionPlayers_Queue", "(\"State\" = 'Waiting' AND \"QueueOrder\" IS NOT NULL AND \"QueueOrder\" > 0) OR (\"State\" IN ('Playing', 'Resting') AND \"QueueOrder\" IS NULL)");
            table.HasCheckConstraint("CK_PlaySessionPlayers_Timestamps", "\"UpdatedAt\" >= \"JoinedAt\"");
        });
        player.HasKey(p => p.Id);
        player.Property(p => p.Id).ValueGeneratedNever();
        player.Property(p => p.DisplayName).HasMaxLength(80).IsRequired();
        player.Property(p => p.NormalizedDisplayName).HasMaxLength(80).IsRequired();
        player.Property(p => p.IdentityType).HasConversion<string>().HasMaxLength(20);
        player.Property(p => p.State).HasConversion<string>().HasMaxLength(20);
        player.HasIndex(p => new { p.SessionId, p.NormalizedDisplayName }).IsUnique();
        player.HasIndex(p => new { p.SessionId, p.QueueOrder }).IsUnique().HasFilter("\"QueueOrder\" IS NOT NULL");
        player.HasIndex(p => new { p.SessionId, p.State, p.QueueOrder });
    }
}
