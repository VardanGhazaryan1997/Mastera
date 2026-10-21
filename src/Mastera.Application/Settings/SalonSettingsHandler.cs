using Mastera.Application.Common;
using Mastera.Application.Salons;
using Mastera.Application.Tenancy;
using Mastera.Domain.Common;

namespace Mastera.Application.Settings;

public sealed record BookingSettingsView(bool HideClientPhoneFromMasters, int CancellationWindowHours);

/// <summary>Tasks 5.6/5.8: owners decide how late clients may change bookings, and whether masters see phone numbers.</summary>
public sealed class SalonSettingsHandler(ITenantContext tenantContext, ITenantRepository tenants, IUnitOfWork unitOfWork)
{
    public async Task<Result<BookingSettingsView>> GetAsync(CancellationToken ct)
    {
        var tenant = tenantContext.Current is { } salon ? await tenants.FindAsync(salon.Id, ct) : null;
        return tenant is null
            ? Result.Failure<BookingSettingsView>(Errors.NoTenant())
            : Result.Success(new BookingSettingsView(tenant.HideClientPhoneFromMasters, tenant.CancellationWindowHours));
    }

    public async Task<Result<BookingSettingsView>> UpdateAsync(BookingSettingsView input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tenant = tenantContext.Current is { } salon ? await tenants.FindAsync(salon.Id, ct) : null;
        if (tenant is null)
        {
            return Result.Failure<BookingSettingsView>(Errors.NoTenant());
        }

        try
        {
            tenant.ChangeBookingSettings(input.HideClientPhoneFromMasters, input.CancellationWindowHours);
        }
        catch (DomainException e)
        {
            return Result.Failure<BookingSettingsView>(Errors.FromDomain(e));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new BookingSettingsView(tenant.HideClientPhoneFromMasters, tenant.CancellationWindowHours));
    }
}
