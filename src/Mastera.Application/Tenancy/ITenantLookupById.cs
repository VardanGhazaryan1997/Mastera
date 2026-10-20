using Mastera.Domain.Tenants;

namespace Mastera.Application.Tenancy;

public interface ITenantLookupById
{
    Task<CurrentTenant?> FindByIdAsync(TenantId id, CancellationToken cancellationToken);
}
