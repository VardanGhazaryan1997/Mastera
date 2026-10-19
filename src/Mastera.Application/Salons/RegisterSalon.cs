using Mastera.Application.Common;
using Mastera.Application.Memberships;
using Mastera.Application.Users;
using Mastera.Domain.Common;
using Mastera.Domain.Memberships;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Application.Salons;

public sealed record RegisterSalonCommand(
    string? SalonName,
    string? Slug,
    string? OwnerName,
    string? Email,
    string? Password,
    string? TimeZone = null,
    string? Currency = null,
    string? Language = null);

public sealed record RegisterSalonResult(TenantId TenantId, TenantSlug Slug, Guid OwnerUserId);

/// <summary>Task 1.6: a salon signs up — creates the salon, the owner's account and the Owner membership in one go.</summary>
public sealed class RegisterSalonHandler(
    ITenantRepository tenants,
    IMembershipRepository memberships,
    IUserAccounts users,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const int OwnerNameMinLength = 2;
    public const int OwnerNameMaxLength = 100;

    public async Task<Result<RegisterSalonResult>> HandleAsync(RegisterSalonCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = clock.GetCurrentInstant();
        Tenant tenant;
        try
        {
            tenant = Tenant.Create(
                command.SalonName,
                TenantSlug.Create(command.Slug),
                command.TimeZone is null ? TimeZoneId.Tbilisi : TimeZoneId.Create(command.TimeZone),
                command.Currency is null ? Domain.Tenants.Currency.Gel : Domain.Tenants.Currency.Create(command.Currency),
                command.Language is null ? Domain.Tenants.Language.Georgian : Domain.Tenants.Language.Create(command.Language),
                now);
        }
        catch (DomainException e)
        {
            return Result.Failure<RegisterSalonResult>(Error.Validation(e.Code, e.Message));
        }

        var ownerName = (command.OwnerName ?? string.Empty).Trim();
        if (ownerName.Length is < OwnerNameMinLength or > OwnerNameMaxLength)
        {
            return Result.Failure<RegisterSalonResult>(Error.Validation(
                "user.name.length", $"Your name must be {OwnerNameMinLength}–{OwnerNameMaxLength} characters long."));
        }

        if (await tenants.SlugExistsAsync(tenant.Slug, cancellationToken))
        {
            return Result.Failure<RegisterSalonResult>(Error.Conflict(
                "tenant.slug.taken", $"The address '{tenant.Slug}' is already taken."));
        }

        return await unitOfWork.InTransactionAsync(
            async ct =>
            {
                var user = await users.CreateAsync(command.Email ?? string.Empty, command.Password ?? string.Empty, ownerName, ct);
                if (!user.IsSuccess)
                {
                    return Result.Failure<RegisterSalonResult>(user.Error!);
                }

                tenants.Add(tenant);
                memberships.Add(Membership.Create(tenant.Id, user.Value, MembershipRole.Owner, now));
                await unitOfWork.SaveChangesAsync(ct);

                return Result.Success(new RegisterSalonResult(tenant.Id, tenant.Slug, user.Value));
            },
            cancellationToken);
    }
}
