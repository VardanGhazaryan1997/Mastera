using Mastera.Application.Tenancy;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Tenancy;

public sealed class TenantLookup(MasteraDbContext db) : ITenantLookup
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
}
