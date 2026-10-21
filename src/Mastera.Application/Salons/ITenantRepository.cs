using Mastera.Domain.Tenants;

namespace Mastera.Application.Salons;

public interface ITenantRepository
{
    Task<bool> SlugExistsAsync(TenantSlug slug, CancellationToken cancellationToken);

    Task<Tenant?> FindAsync(TenantId id, CancellationToken cancellationToken);

    void Add(Tenant tenant);
}
