using Mastera.Domain.Memberships;
using Mastera.Domain.Tenants;

namespace Mastera.Application.Memberships;

/// <summary>A salon the user belongs to, with their role there.</summary>
public sealed record MembershipView(TenantId TenantId, TenantSlug Slug, string SalonName, MembershipRole Role);

/// <summary>A person working in a salon.</summary>
public sealed record MemberView(Guid UserId, string Email, string DisplayName, MembershipRole Role);

public interface IMembershipRepository
{
    void Add(Membership membership);

    Task<IReadOnlyList<MembershipView>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<MembershipView?> FindAsync(Guid userId, TenantSlug slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberView>> ListMembersAsync(TenantId tenantId, CancellationToken cancellationToken);
}
