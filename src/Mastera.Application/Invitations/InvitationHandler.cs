using Mastera.Application.Common;
using Mastera.Application.Email;
using Mastera.Application.Masters;
using Mastera.Application.Memberships;
using Mastera.Application.Tenancy;
using Mastera.Application.Users;
using Mastera.Domain.Common;
using Mastera.Domain.Invitations;
using Mastera.Domain.Masters;
using Mastera.Domain.Memberships;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Application.Invitations;

public sealed record InviteInput(string? Email, MembershipRole Role, Guid? MasterId);

public sealed record InvitationView(string SalonName, string SalonSlug, string Email, string Role, bool Expired, bool Accepted, bool HasAccount);

/// <summary>How the invitee proves who they are: already signed in, or a new account.</summary>
public sealed record AcceptInput(string Token, Guid? SignedInUserId, string? SignedInEmail, string? DisplayName, string? Password);

public sealed record AcceptResult(Guid UserId, TenantSlug Slug, bool AccountCreated);

/// <summary>Task 2.2: invite people to a salon by email; accepting creates the membership (and links the master).</summary>
public sealed class InvitationHandler(
    IInvitationRepository invitations,
    IMembershipRepository memberships,
    IMasterRepository masters,
    IUserAccounts users,
    IEmailSender email,
    IInvitationLinks links,
    ITenantContext tenantContext,
    ITenantSwitcher tenantSwitcher,
    ITenantLookupById tenantLookup,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const string SignInRequiredCode = "invitation.sign_in_required";

    public async Task<Result<bool>> InviteAsync(InviteInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (tenantContext.Current is not { } salon)
        {
            return Result.Failure<bool>(Errors.NoTenant());
        }

        MasterId? masterId = input.MasterId is { } id ? new MasterId(id) : null;
        if (masterId is { } mid && await masters.FindAsync(mid, ct) is null)
        {
            return Result.Failure<bool>(Error.NotFound("master.not_found", "The master was not found."));
        }

        var token = InvitationToken.New();
        Invitation invitation;
        try
        {
            invitation = Invitation.Create(salon.Id, input.Email, input.Role, masterId, InvitationToken.Hash(token), clock.GetCurrentInstant());
        }
        catch (DomainException e)
        {
            return Result.Failure<bool>(Errors.FromDomain(e));
        }

        invitations.Add(invitation);
        await unitOfWork.SaveChangesAsync(ct);

        var url = links.AcceptUrl(token);
        await email.SendAsync(
            new EmailMessage(
                invitation.Email,
                $"You're invited to {salon.Name} on Mastera",
                $"""
                Hello,

                {salon.Name} invited you to join their team on Mastera as {invitation.Role}.

                Accept the invitation: {url}

                The link works for 7 days.
                """),
            ct);

        return Result.Success(true);
    }

    public async Task<Result<InvitationView>> GetAsync(string token, CancellationToken ct)
    {
        var invitation = await invitations.FindByTokenHashAsync(InvitationToken.Hash(token ?? string.Empty), ct);
        if (invitation is null)
        {
            return Result.Failure<InvitationView>(InvitationNotFound());
        }

        var salon = await tenantLookup.FindByIdAsync(invitation.TenantId, ct);
        if (salon is null)
        {
            return Result.Failure<InvitationView>(InvitationNotFound());
        }

        var hasAccount = await users.FindIdByEmailAsync(invitation.Email, ct) is not null;
        return Result.Success(new InvitationView(
            salon.Name,
            salon.Slug.Value,
            invitation.Email,
            invitation.Role.ToString(),
            invitation.IsExpired(clock.GetCurrentInstant()),
            invitation.IsAccepted,
            hasAccount));
    }

    public async Task<Result<AcceptResult>> AcceptAsync(AcceptInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var invitation = await invitations.FindByTokenHashAsync(InvitationToken.Hash(input.Token ?? string.Empty), ct);
        if (invitation is null)
        {
            return Result.Failure<AcceptResult>(InvitationNotFound());
        }

        var now = clock.GetCurrentInstant();
        if (invitation.IsAccepted)
        {
            return Result.Failure<AcceptResult>(Error.Conflict("invitation.used", "This invitation has already been used."));
        }

        if (invitation.IsExpired(now))
        {
            return Result.Failure<AcceptResult>(Error.Validation("invitation.expired", "This invitation has expired. Ask the salon for a new one."));
        }

        if (input.SignedInUserId is not null
            && !string.Equals(input.SignedInEmail?.Trim(), invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<AcceptResult>(new Error(
                "invitation.email_mismatch",
                $"This invitation is for {invitation.Email}. Sign out and sign in with that email.",
                ErrorKind.Forbidden));
        }

        var existingUserId = input.SignedInUserId ?? await users.FindIdByEmailAsync(invitation.Email, ct);
        if (input.SignedInUserId is null && existingUserId is not null)
        {
            return Result.Failure<AcceptResult>(new Error(
                SignInRequiredCode,
                $"There is already an account for {invitation.Email}. Sign in to accept.",
                ErrorKind.Unauthorized));
        }

        var salon = await tenantLookup.FindByIdAsync(invitation.TenantId, ct);
        if (salon is null)
        {
            return Result.Failure<AcceptResult>(InvitationNotFound());
        }

        // From here on, work inside the salon: its masters are tenant-owned.
        tenantSwitcher.Use(salon);

        return await unitOfWork.InTransactionAsync(
            async token =>
            {
                var accountCreated = false;
                var userId = existingUserId;
                if (userId is null)
                {
                    var created = await users.CreateAsync(invitation.Email, input.Password ?? string.Empty, input.DisplayName ?? string.Empty, token);
                    if (!created.IsSuccess)
                    {
                        return Result.Failure<AcceptResult>(created.Error!);
                    }

                    userId = created.Value;
                    accountCreated = true;
                }

                try
                {
                    invitation.Accept(userId.Value, now);

                    if (await memberships.FindAsync(userId.Value, salon.Slug, token) is null)
                    {
                        memberships.Add(Membership.Create(invitation.TenantId, userId.Value, invitation.Role, now));
                    }

                    if (invitation.MasterId is { } masterId && await masters.FindAsync(masterId, token) is { } master)
                    {
                        master.LinkUser(userId.Value);
                    }
                }
                catch (DomainException e)
                {
                    return Result.Failure<AcceptResult>(Errors.FromDomain(e));
                }

                await unitOfWork.SaveChangesAsync(token);
                return Result.Success(new AcceptResult(userId.Value, salon.Slug, accountCreated));
            },
            ct);
    }

    private static Error InvitationNotFound() =>
        Error.NotFound("invitation.not_found", "This invitation link is not valid.");
}
