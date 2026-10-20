using Mastera.Application.Tenancy;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Mastera.Infrastructure.Tenancy;

public sealed class TenantLookup(MasteraDbContext db) : ITenantLookup, ITenantLookupById, ITenantSettings
{
    public async Task<CurrentTenant?> FindBySlugAsync(TenantSlug slug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var tenant = await db.Tenants
            .AsNoTracking()
            .Where(t => t.Slug == slug)
            .Select(t => new { t.Id, t.Slug, t.Name })
            .SingleOrDefaultAsync(cancellationToken);

        return tenant is null ? null : new CurrentTenant(tenant.Id, tenant.Slug, tenant.Name);
    }

    public async Task<CurrentTenant?> FindByIdAsync(TenantId id, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new { t.Id, t.Slug, t.Name })
            .SingleOrDefaultAsync(cancellationToken);

        return tenant is null ? null : new CurrentTenant(tenant.Id, tenant.Slug, tenant.Name);
    }

    public async Task<DateTimeZone> GetTimeZoneAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var zone = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.TimeZone)
            .SingleAsync(cancellationToken);
        return zone.ToDateTimeZone();
    }
}
