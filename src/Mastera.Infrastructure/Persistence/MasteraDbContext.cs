using System.Reflection;
using Mastera.Application.Tenancy;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Persistence;

public class MasteraDbContext : DbContext
{
    /// <summary>Used when there is no current tenant. No row ever has this id, so queries return nothing.</summary>
    public static readonly TenantId NoTenant = default;

    private static readonly MethodInfo ConfigureTenantOwnedMethod = typeof(MasteraDbContext)
        .GetMethod(nameof(ConfigureTenantOwned), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ITenantContext _tenantContext;

    public MasteraDbContext(DbContextOptions<MasteraDbContext> options, ITenantContext tenantContext)
        : this((DbContextOptions)options, tenantContext)
    {
    }

    /// <summary>For derived contexts (used by integration tests).</summary>
    protected MasteraDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary>
    /// The tenant every query is filtered by and every write is stamped with.
    /// Read by EF Core each time a query runs, and by the connection interceptor for row-level security.
    /// </summary>
    public TenantId CurrentTenantId => _tenantContext.Current?.Id ?? NoTenant;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<TenantId>().HaveConversion<TenantIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasteraDbContext).Assembly);

        var tenantOwned = modelBuilder.Model.GetEntityTypes()
            .Where(t => t.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        foreach (var clrType in tenantOwned)
        {
            ConfigureTenantOwnedMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ConfigureTenantOwned<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        entity.HasIndex(nameof(ITenantOwned.TenantId));
    }
}
