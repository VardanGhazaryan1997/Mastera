using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Persistence;

public sealed class MasteraDbContext(DbContextOptions<MasteraDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasteraDbContext).Assembly);
    }
}
