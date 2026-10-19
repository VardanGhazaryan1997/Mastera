using Mastera.Application.Salons;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Repositories;

public sealed class TenantRepository(MasteraDbContext db) : ITenantRepository
{
    public Task<bool> SlugExistsAsync(TenantSlug slug, CancellationToken cancellationToken) =>
        db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken);

    public void Add(Tenant tenant) => db.Tenants.Add(tenant);
}
