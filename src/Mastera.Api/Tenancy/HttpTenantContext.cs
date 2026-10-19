using Mastera.Application.Tenancy;

namespace Mastera.Api.Tenancy;

/// <summary>Per-request tenant context, filled in once by <see cref="TenantResolutionMiddleware"/>.</summary>
public sealed class HttpTenantContext : ITenantContext
{
    public CurrentTenant? Current { get; private set; }

    public void Set(CurrentTenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (Current is not null && Current.Id != tenant.Id)
        {
            throw new TenantIsolationException("The tenant of a request cannot change once it is set.");
        }

        Current = tenant;
    }
}
