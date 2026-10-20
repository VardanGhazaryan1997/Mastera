using Mastera.Domain.Bookings;
using Mastera.Domain.Masters;
using NodaTime;

namespace Mastera.Application.Bookings;

public sealed record BookingView(
    Guid Id,
    Guid MasterId,
    string MasterName,
    Guid ServiceId,
    string ServiceName,
    Guid? ClientId,
    string ClientName,
    string? ClientPhone,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Status,
    string Source,
    decimal Price,
    decimal? FinalPrice,
    string? Note);

public sealed record BlockedTimeView(Guid Id, Guid MasterId, DateTimeOffset Start, DateTimeOffset End, string? Reason);

public sealed record CalendarView(IReadOnlyList<BookingView> Bookings, IReadOnlyList<BlockedTimeView> Blocked);

/// <summary>Bookings, holds and blocked time of the current salon.</summary>
public interface IBookingRepository
{
    void Add(Booking booking);

    void Add(SlotHold hold);

    void Add(BlockedTime blocked);

    Task<Booking?> FindAsync(BookingId id, CancellationToken cancellationToken);

    Task<SlotHold?> FindHoldAsync(SlotHoldId id, CancellationToken cancellationToken);

    Task<BlockedTime?> FindBlockedAsync(BlockedTimeId id, CancellationToken cancellationToken);

    void Remove(SlotHold hold);

    void Remove(BlockedTime blocked);

    /// <summary>Deletes a master's expired holds so their time is free again.</summary>
    Task RemoveExpiredHoldsAsync(MasterId masterId, Instant now, CancellationToken cancellationToken);

    /// <summary>
    /// True if the master has an active booking, an unexpired hold (other than <paramref name="ignoreHold"/>)
    /// or blocked time overlapping the range.
    /// </summary>
    Task<bool> IsBusyAsync(MasterId masterId, Instant start, Instant end, Instant now, SlotHoldId? ignoreHold, CancellationToken cancellationToken);

    Task<CalendarView> ListAsync(Instant from, Instant to, MasterId? masterId, CancellationToken cancellationToken);
}
