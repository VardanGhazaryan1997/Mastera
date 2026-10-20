using Mastera.Domain.Catalog;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Masters;

/// <summary>A service a master offers, optionally at their own price or duration (a senior stylist may cost more).</summary>
public sealed class MasterService : ITenantOwned
{
    internal MasterService(MasterId masterId, ServiceId serviceId, decimal? priceOverride, Duration? durationOverride)
    {
        MasterId = masterId;
        ServiceId = serviceId;
        PriceOverride = priceOverride;
        DurationOverride = durationOverride;
    }

    private MasterService()
    {
    }

    public TenantId TenantId { get; private set; }

    public MasterId MasterId { get; private set; }

    public ServiceId ServiceId { get; private set; }

    public decimal? PriceOverride { get; private set; }

    public Duration? DurationOverride { get; private set; }

    public decimal EffectivePrice(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        EnsureSameService(service);
        return PriceOverride ?? service.Price;
    }

    public Duration EffectiveDuration(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        EnsureSameService(service);
        return DurationOverride ?? service.Duration;
    }

    internal void ChangeOverrides(decimal? priceOverride, Duration? durationOverride)
    {
        PriceOverride = priceOverride;
        DurationOverride = durationOverride;
    }

    private void EnsureSameService(Service service)
    {
        if (service.Id != ServiceId)
        {
            throw new ArgumentException("The service does not match this master's offer.", nameof(service));
        }
    }
}
