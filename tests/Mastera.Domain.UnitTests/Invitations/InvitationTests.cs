using Mastera.Domain.Common;
using Mastera.Domain.Invitations;
using Mastera.Domain.Masters;
using Mastera.Domain.Memberships;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.UnitTests.Invitations;

public sealed class InvitationTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 20, 9, 0);

    private static Invitation Invite(MembershipRole role = MembershipRole.Master, MasterId? master = null) =>
        Invitation.Create(TenantId.New(), " Luka@Example.GE ", role, master, "hash", Now);

    [Fact]
    public void Create_normalizes_the_email_and_expires_in_7_days()
    {
        var invitation = Invite();

        invitation.Email.ShouldBe("luka@example.ge");
        invitation.ExpiresAt.ShouldBe(Now + Duration.FromDays(7));
        invitation.IsAccepted.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("luka")]
    [InlineData("luka@")]
    [InlineData("@example.ge")]
    [InlineData("luka@example")]
    [InlineData("lu ka@example.ge")]
    [InlineData("luka@@example.ge")]
    public void Create_rejects_invalid_emails(string? email)
    {
        Should.Throw<DomainException>(() => Invitation.Create(TenantId.New(), email, MembershipRole.Admin, null, "hash", Now))
            .Code.ShouldBe("invitation.email.invalid");
    }

    [Fact]
    public void Nobody_can_be_invited_as_owner()
    {
        Should.Throw<DomainException>(() => Invite(MembershipRole.Owner)).Code.ShouldBe("invitation.role.invalid");
    }

    [Fact]
    public void Only_masters_can_be_linked_to_a_master_profile()
    {
        Should.Throw<DomainException>(() => Invite(MembershipRole.Admin, MasterId.New())).Code.ShouldBe("invitation.role.invalid");
        Invite(MembershipRole.Master, MasterId.New()).MasterId.ShouldNotBeNull();
    }

    [Fact]
    public void Accept_records_who_and_when()
    {
        var invitation = Invite();
        var user = Guid.NewGuid();

        invitation.Accept(user, Now + Duration.FromHours(1));

        invitation.IsAccepted.ShouldBeTrue();
        invitation.AcceptedByUserId.ShouldBe(user);
        invitation.AcceptedAt.ShouldBe(Now + Duration.FromHours(1));
    }

    [Fact]
    public void An_invitation_can_be_used_once()
    {
        var invitation = Invite();
        invitation.Accept(Guid.NewGuid(), Now);

        Should.Throw<DomainException>(() => invitation.Accept(Guid.NewGuid(), Now)).Code.ShouldBe("invitation.used");
    }

    [Fact]
    public void An_expired_invitation_cannot_be_accepted()
    {
        var invitation = Invite();

        Should.Throw<DomainException>(() => invitation.Accept(Guid.NewGuid(), Now + Duration.FromDays(7)))
            .Code.ShouldBe("invitation.expired");
    }
}
