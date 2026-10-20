using Mastera.Domain.Availability;
using Mastera.Domain.Masters;
using NodaTime;

namespace Mastera.Application.Availability;

/// <summary>
/// Bookings, slot holds and blocked time per master. Bookings arrive in Epic 5; until then the
/// Infrastructure implementation returns nothing.
/// </summary>
public interface IBusyTimeSource
{
    Task<IReadOnlyDictionary<MasterId, IReadOnlyList<BusyInterval>>> GetBusyAsync(
        IReadOnlyCollection<MasterId> masterIds,
        Instant from,
        Instant to,
        CancellationToken cancellationToken);
}
