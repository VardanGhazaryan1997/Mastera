using Mastera.Domain.Common;
using Mastera.Domain.Memberships;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.UnitTests.Memberships;

public sealed class MembershipTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 20, 9, 0);

    [Fact]
    public void Create_links_a_user_to_a_salon_with_a_role()
    {
        var tenant = TenantId.New();
        var user = Guid.NewGuid();

        var membership = Membership.Create(tenant, user, MembershipRole.Owner, Now);

        membership.Id.Value.ShouldNotBe(Guid.Empty);
        membership.TenantId.ShouldBe(tenant);
        membership.UserId.ShouldBe(user);
        membership.Role.ShouldBe(MembershipRole.Owner);
        membership.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Create_requires_a_salon()
    {
        Should.Throw<DomainException>(() => Membership.Create(default, Guid.NewGuid(), MembershipRole.Master, Now))
            .Code.ShouldBe("membership.tenant.required");
    }

    [Fact]
    public void Create_requires_a_user()
    {
        Should.Throw<DomainException>(() => Membership.Create(TenantId.New(), Guid.Empty, MembershipRole.Master, Now))
            .Code.ShouldBe("membership.user.required");
    }

    [Fact]
    public void Create_rejects_an_unknown_role()
    {
        Should.Throw<DomainException>(() => Membership.Create(TenantId.New(), Guid.NewGuid(), (MembershipRole)42, Now))
            .Code.ShouldBe("membership.role.invalid");
    }

    [Fact]
    public void ChangeRole_changes_the_role()
    {
        var membership = Membership.Create(TenantId.New(), Guid.NewGuid(), MembershipRole.Master, Now);

        membership.ChangeRole(MembershipRole.Admin);

        membership.Role.ShouldBe(MembershipRole.Admin);
    }

    [Theory]
    [InlineData(MembershipRole.Owner, true)]
    [InlineData(MembershipRole.Admin, true)]
    [InlineData(MembershipRole.Receptionist, false)]
    [InlineData(MembershipRole.Master, false)]
    public void Only_owners_and_admins_manage_the_salon(MembershipRole role, bool expected)
    {
        role.CanManageSalon().ShouldBe(expected);
    }
}
