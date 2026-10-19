using Mastera.Application.Tenancy;
using Mastera.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mastera.Infrastructure.Persistence;

/// <summary>
/// Before saving: stamps new tenant-owned rows with the current tenant, and refuses to insert, update
/// or delete rows of any other tenant.
/// </summary>
public sealed class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    public static readonly TenantSaveChangesInterceptor Instance = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Apply(DbContext? context)
    {
        if (context is not MasteraDbContext db)
        {
            return;
        }

        foreach (var entry in db.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            var entityName = entry.Metadata.ClrType.Name;
            var current = db.CurrentTenantId;
            if (current == MasteraDbContext.NoTenant)
            {
                throw new TenantIsolationException($"Cannot save {entityName} without a current tenant.");
            }

            var property = entry.Property(nameof(ITenantOwned.TenantId));
            var value = (TenantId)property.CurrentValue!;

            if (entry.State == EntityState.Added && value == MasteraDbContext.NoTenant)
            {
                property.CurrentValue = current;
                continue;
            }

            var original = entry.State == EntityState.Added ? value : (TenantId)property.OriginalValue!;
            if (value != current || original != current)
            {
                throw new TenantIsolationException(
                    $"Cannot {entry.State.ToString().ToUpperInvariant()} {entityName} of tenant {original} while working for tenant {current}.");
            }
        }
    }
}
