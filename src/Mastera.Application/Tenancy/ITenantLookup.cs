using Mastera.Domain.Tenants;

namespace Mastera.Application.Tenancy;

public interface ITenantLookup
{
    Task<CurrentTenant?> FindBySlugAsync(TenantSlug slug, CancellationToken cancellationToken);
}
