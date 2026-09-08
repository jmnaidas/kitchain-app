using Microsoft.EntityFrameworkCore;
using Kitchain.Domain.Courts;

namespace Kitchain.Infrastructure.Persistence;

public sealed class KitchainDbContext(DbContextOptions<KitchainDbContext> options)
    : DbContext(options)
{
    public DbSet<Court> Courts => Set<Court>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KitchainDbContext).Assembly);
}
