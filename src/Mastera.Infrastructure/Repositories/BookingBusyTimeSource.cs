using Mastera.Application.Availability;
using Mastera.Domain.Availability;
using Mastera.Domain.Bookings;
using Mastera.Domain.Masters;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Mastera.Infrastructure.Repositories;

/// <summary>Task 4.2/5.2: what makes a master busy — active bookings, unexpired holds and blocked time.</summary>
public sealed class BookingBusyTimeSource(MasteraDbContext db, IClock clock) : IBusyTimeSource
{
    public async Task<IReadOnlyDictionary<MasterId, IReadOnlyList<BusyInterval>>> GetBusyAsync(
        IReadOnlyCollection<MasterId> masterIds, Instant from, Instant to, BookingId? ignoreBooking, CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var ids = masterIds.ToList();

        var bookingQuery = db.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.MasterId)
                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                && b.Start < to && from < b.End);
        if (ignoreBooking is { } ignored)
        {
            bookingQuery = bookingQuery.Where(b => b.Id != ignored);
        }

        var bookings = await bookingQuery
            .Select(b => new { b.MasterId, b.Start, b.End })
            .ToListAsync(cancellationToken);

        var holds = await db.SlotHolds.AsNoTracking()
            .Where(h => ids.Contains(h.MasterId) && h.ExpiresAt > now && h.Start < to && from < h.End)
            .Select(h => new { h.MasterId, h.Start, h.End })
            .ToListAsync(cancellationToken);

        var blocked = await db.BlockedTimes.AsNoTracking()
            .Where(b => ids.Contains(b.MasterId) && b.Start < to && from < b.End)
            .Select(b => new { b.MasterId, b.Start, b.End })
            .ToListAsync(cancellationToken);

        return bookings.Concat(holds).Concat(blocked)
            .GroupBy(x => x.MasterId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<BusyInterval>)g.Select(x => new BusyInterval(x.Start, x.End)).OrderBy(x => x.Start).ToList());
    }
}
