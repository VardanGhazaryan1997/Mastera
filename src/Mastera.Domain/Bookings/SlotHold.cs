using Mastera.Domain.Catalog;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Bookings;

/// <summary>
/// Task 5.3: keeps a slot for a client while they fill in their details, so nobody else books it meanwhile.
/// Expires after <see cref="Lifetime"/>.
/// </summary>
public sealed class SlotHold : Entity<SlotHoldId>, ITenantOwned
{
    public static readonly Duration Lifetime = Duration.FromMinutes(10);

    private SlotHold(SlotHoldId id)
        : base(id)
    {
    }

    private SlotHold()
        : base(default)
    {
    }

    public TenantId TenantId { get; private set; }

    public MasterId MasterId { get; private set; }

    public ServiceId ServiceId { get; private set; }

    public Instant Start { get; private set; }

    public Instant End { get; private set; }

    public decimal Price { get; private set; }

    public Instant ExpiresAt { get; private set; }

    public static SlotHold Create(MasterId masterId, ServiceId serviceId, Instant start, Duration duration, decimal price, Instant now)
    {
        if (duration <= Duration.Zero)
        {
            throw new DomainException("booking.duration.invalid", "A booking must last longer than zero minutes.");
        }

        return new SlotHold(SlotHoldId.New())
        {
            MasterId = masterId,
            ServiceId = serviceId,
            Start = start,
            End = start + duration,
            Price = price,
            ExpiresAt = now + Lifetime,
        };
    }

    public bool IsExpired(Instant now) => now >= ExpiresAt;
}
