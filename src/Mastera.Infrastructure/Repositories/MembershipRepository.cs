using Mastera.Application.Memberships;
using Mastera.Domain.Memberships;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Repositories;

public sealed class MembershipRepository(MasteraDbContext db) : IMembershipRepository
{
    public void Add(Membership membership) => db.Memberships.Add(membership);

    public async Task<IReadOnlyList<MembershipView>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await (from m in db.Memberships.AsNoTracking()
               join t in db.Tenants.AsNoTracking() on m.TenantId equals t.Id
               where m.UserId == userId
               orderby t.Name
               select new MembershipView(t.Id, t.Slug, t.Name, m.Role))
            .ToListAsync(cancellationToken);

    public Task<MembershipView?> FindAsync(Guid userId, TenantSlug slug, CancellationToken cancellationToken) =>
        (from m in db.Memberships.AsNoTracking()
         join t in db.Tenants.AsNoTracking() on m.TenantId equals t.Id
         where m.UserId == userId && t.Slug == slug
         select new MembershipView(t.Id, t.Slug, t.Name, m.Role))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MemberView>> ListMembersAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await (from m in db.Memberships.AsNoTracking()
               join u in db.Users.AsNoTracking() on m.UserId equals u.Id
               where m.TenantId == tenantId
               orderby u.DisplayName
               select new MemberView(u.Id, u.Email ?? string.Empty, u.DisplayName, m.Role))
            .ToListAsync(cancellationToken);
}
