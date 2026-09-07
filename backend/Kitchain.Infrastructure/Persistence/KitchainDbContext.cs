using Microsoft.EntityFrameworkCore;

namespace Kitchain.Infrastructure.Persistence;

public sealed class KitchainDbContext(DbContextOptions<KitchainDbContext> options)
    : DbContext(options)
{
}
