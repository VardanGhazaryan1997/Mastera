using Mastera.Domain.Availability;
using Mastera.Domain.Bookings;
using Mastera.Domain.Masters;
using NodaTime;

namespace Mastera.Application.Availability;

/// <summary>Bookings, slot holds and blocked time per master.</summary>
public interface IBusyTimeSource
{
    /// <param name="ignoreBooking">A booking that is being moved: its current time counts as free (task 5.6).</param>
    Task<IReadOnlyDictionary<MasterId, IReadOnlyList<BusyInterval>>> GetBusyAsync(
        IReadOnlyCollection<MasterId> masterIds,
        Instant from,
        Instant to,
        BookingId? ignoreBooking,
        CancellationToken cancellationToken);
}
