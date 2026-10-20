using System.Diagnostics.CodeAnalysis;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Memberships;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Invitations;

/// <summary>
/// An email invitation to join a salon with a role (optionally as a specific master).
/// Platform-level (not <see cref="ITenantOwned"/>): it is opened by someone who isn't in the salon yet.
/// Only a hash of the secret link token is stored.
/// </summary>
public sealed class Invitation : Entity<InvitationId>
{
    public const int EmailMaxLength = 256;

    public static readonly Duration Lifetime = Duration.FromDays(7);

    private Invitation(InvitationId id, TenantId tenantId, string email, MembershipRole role, MasterId? masterId, string tokenHash, Instant now)
        : base(id)
    {
        TenantId = tenantId;
        Email = email;
        Role = role;
        MasterId = masterId;
        TokenHash = tokenHash;
        CreatedAt = now;
        ExpiresAt = now + Lifetime;
    }

    private Invitation()
        : base(default)
    {
        Email = string.Empty;
        TokenHash = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public string Email { get; private set; }

    public MembershipRole Role { get; private set; }

    public MasterId? MasterId { get; private set; }

    public string TokenHash { get; private set; }

    public Instant CreatedAt { get; private init; }

    public Instant ExpiresAt { get; private set; }

    public Instant? AcceptedAt { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    public static Invitation Create(TenantId tenantId, string? email, MembershipRole role, MasterId? masterId, string tokenHash, Instant now)
    {
        if (tenantId == default)
        {
            throw new DomainException("invitation.tenant.required", "An invitation needs a salon.");
        }

        if (role == MembershipRole.Owner || !Enum.IsDefined(role))
        {
            throw new DomainException("invitation.role.invalid", "People can be invited as Admin, Receptionist or Master.");
        }

        if (masterId is not null && role != MembershipRole.Master)
        {
            throw new DomainException("invitation.role.invalid", "Only masters can be linked to a master profile.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return new Invitation(InvitationId.New(), tenantId, NormalizeEmail(email), role, masterId, tokenHash, now);
    }

    public bool IsExpired(Instant now) => now >= ExpiresAt;

    public bool IsAccepted => AcceptedAt is not null;

    public void Accept(Guid userId, Instant now)
    {
        if (IsAccepted)
        {
            throw new DomainException("invitation.used", "This invitation has already been used.");
        }

        if (IsExpired(now))
        {
            throw new DomainException("invitation.expired", "This invitation has expired. Ask the salon for a new one.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("invitation.user.required", "A user is required.");
        }

        AcceptedAt = now;
        AcceptedByUserId = userId;
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Email addresses are compared in lowercase.")]
    public static string NormalizeEmail(string? email)
    {
        var value = (email ?? string.Empty).Trim().ToLowerInvariant();
        var at = value.IndexOf('@', StringComparison.Ordinal);
        var valid = value.Length <= EmailMaxLength
            && at > 0
            && at == value.LastIndexOf('@')
            && value.IndexOf('.', at) > at + 1
            && !value.EndsWith('.')
            && !value.Any(char.IsWhiteSpace);

        if (!valid)
        {
            throw new DomainException("invitation.email.invalid", "Enter a valid email address.");
        }

        return value;
    }
}
