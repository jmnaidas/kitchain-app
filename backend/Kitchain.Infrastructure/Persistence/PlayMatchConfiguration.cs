using Kitchain.Domain.Play;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitchain.Infrastructure.Persistence;

internal sealed class PlayMatchConfiguration : IEntityTypeConfiguration<PlayMatch>
{
    public void Configure(EntityTypeBuilder<PlayMatch> match)
    {
        match.ToTable("PlayMatches", table =>
        {
            table.HasCheckConstraint("CK_PlayMatches_Court", "\"CourtNumber\" > 0");
            table.HasCheckConstraint("CK_PlayMatches_Status", "(\"Status\" = 'Active' AND \"CompletedAt\" IS NULL) OR (\"Status\" = 'Completed' AND \"CompletedAt\" >= \"StartedAt\" AND \"CompletedAt\" IS NOT NULL)");
        });
        match.HasKey(m => m.Id);
        match.Property(m => m.Id).ValueGeneratedNever();
        match.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        match.HasIndex(m => new { m.SessionId, m.CourtNumber }).IsUnique().HasFilter("\"Status\" = 'Active'");
        match.HasMany(m => m.Players).WithOne().HasForeignKey(p => p.MatchId).OnDelete(DeleteBehavior.Cascade);
        match.Navigation(m => m.Players).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PlayMatchPlayerConfiguration : IEntityTypeConfiguration<PlayMatchPlayer>
{
    public void Configure(EntityTypeBuilder<PlayMatchPlayer> player)
    {
        player.ToTable("PlayMatchPlayers", table =>
            table.HasCheckConstraint("CK_PlayMatchPlayers_TeamPosition", "(\"Position\" IN (1, 2) AND \"Team\" = 'A') OR (\"Position\" IN (3, 4) AND \"Team\" = 'B')"));
        player.HasKey(p => new { p.MatchId, p.PlayerId });
        player.Property(p => p.Team).HasConversion<string>().HasMaxLength(1);
        player.HasIndex(p => new { p.MatchId, p.Position }).IsUnique();
        player.HasOne<PlaySessionPlayer>().WithMany().HasForeignKey(p => p.PlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}
