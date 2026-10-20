using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Application.Tenancy;

public interface ITenantSettings
{
    Task<DateTimeZone> GetTimeZoneAsync(TenantId tenantId, CancellationToken cancellationToken);
}
