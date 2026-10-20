using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Bookings;

/// <summary>Task 5.5: time a master can't be booked inside working hours (lunch, personal, training).</summary>
public sealed class BlockedTime : Entity<BlockedTimeId>, ITenantOwned
{
    public const int ReasonMaxLength = 200;

    public static readonly Duration MaxLength = Duration.FromDays(1);

    private BlockedTime(BlockedTimeId id)
        : base(id)
    {
    }

    private BlockedTime()
        : base(default)
    {
    }

    public TenantId TenantId { get; private set; }

    public MasterId MasterId { get; private set; }

    public Instant Start { get; private set; }

    public Instant End { get; private set; }

    public string? Reason { get; private set; }

    public Instant CreatedAt { get; private init; }

    public static BlockedTime Create(MasterId masterId, Instant start, Instant end, string? reason, Instant now)
    {
        if (end <= start || end - start > MaxLength)
        {
            throw new DomainException("blocked.range.invalid", "Blocked time must end after it starts and last at most a day. Use a day off for longer.");
        }

        return new BlockedTime(BlockedTimeId.New())
        {
            MasterId = masterId,
            Start = start,
            End = end,
            Reason = Text.Optional(reason, ReasonMaxLength, "blocked.reason.length", "The reason"),
            CreatedAt = now,
        };
    }
}
