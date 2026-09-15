using Kitchain.Domain.Play;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitchain.Infrastructure.Persistence;

internal sealed class PlayRallyEventConfiguration : IEntityTypeConfiguration<PlayRallyEvent>
{
    public void Configure(EntityTypeBuilder<PlayRallyEvent> rally)
    {
        rally.ToTable("PlayRallyEvents", table =>
        {
            table.HasCheckConstraint("CK_PlayRallyEvents_Sequence", "\"Sequence\" > 0");
            table.HasCheckConstraint("CK_PlayRallyEvents_Winner", "\"Winner\" IN ('A', 'B')");
            table.HasCheckConstraint("CK_PlayRallyEvents_Scores", "\"TeamAScore\" >= 0 AND \"TeamBScore\" >= 0");
            table.HasCheckConstraint("CK_PlayRallyEvents_Service", "\"ServingTeam\" IN ('A', 'B') AND \"CurrentServerNumber\" IN (1, 2)");
            table.HasCheckConstraint("CK_PlayRallyEvents_CallOut", "\"CallOut\" IS NULL OR \"CallOut\" IN ('Drive', 'Dink', 'Lob', 'Fault', 'Out', 'Kitchen', 'ServiceBreak')");
        });
        rally.HasKey(r => r.Id);
        rally.Property(r => r.Id).ValueGeneratedNever();
        rally.Property(r => r.Winner).HasConversion<string>().HasMaxLength(1);
        rally.Property(r => r.ServingTeam).HasConversion<string>().HasMaxLength(1);
        rally.Property(r => r.CallOut).HasConversion<string>().HasMaxLength(30);
        rally.HasIndex(r => new { r.MatchId, r.Sequence }).IsUnique();
    }
}
