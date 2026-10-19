using Mastera.Domain.Common;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Memberships;

/// <summary>
/// Links a user account to a salon with a role.
/// Platform-level record (not <see cref="ITenantOwned"/>): a user must be able to list all of their salons
/// before choosing one. Queries for a salon's members always filter by tenant explicitly.
/// </summary>
public sealed class Membership : Entity<MembershipId>
{
    private Membership(MembershipId id, TenantId tenantId, Guid userId, MembershipRole role, Instant createdAt)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        CreatedAt = createdAt;
    }

    // For EF Core materialization only.
    private Membership()
        : base(default)
    {
    }

    public TenantId TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public MembershipRole Role { get; private set; }

    public Instant CreatedAt { get; private init; }

    public static Membership Create(TenantId tenantId, Guid userId, MembershipRole role, Instant now)
    {
        if (tenantId == default)
        {
            throw new DomainException("membership.tenant.required", "A membership needs a salon.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("membership.user.required", "A membership needs a user.");
        }

        EnsureDefined(role);
        return new Membership(MembershipId.New(), tenantId, userId, role, now);
    }

    public void ChangeRole(MembershipRole role)
    {
        EnsureDefined(role);
        Role = role;
    }

    private static void EnsureDefined(MembershipRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException("membership.role.invalid", $"'{role}' is not a role.");
        }
    }
}
