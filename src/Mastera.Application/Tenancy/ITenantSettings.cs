using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Application.Tenancy;

/// <summary>Tasks 5.6/5.8: per-salon booking rules.</summary>
public sealed record BookingSettings(bool HideClientPhoneFromMasters, int CancellationWindowHours, Language Language)
{
    public Duration CancellationWindow => Duration.FromHours(CancellationWindowHours);
}

public interface ITenantSettings
{
    Task<DateTimeZone> GetTimeZoneAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<BookingSettings> GetBookingSettingsAsync(TenantId tenantId, CancellationToken cancellationToken);
}
