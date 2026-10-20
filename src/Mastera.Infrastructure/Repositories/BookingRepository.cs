using Mastera.Application.Bookings;
using Mastera.Domain.Bookings;
using Mastera.Domain.Masters;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Mastera.Infrastructure.Repositories;

public sealed class BookingRepository(MasteraDbContext db) : IBookingRepository
{
    public void Add(Booking booking) => db.Bookings.Add(booking);

    public void Add(SlotHold hold) => db.SlotHolds.Add(hold);

    public void Add(BlockedTime blocked) => db.BlockedTimes.Add(blocked);

    public Task<Booking?> FindAsync(BookingId id, CancellationToken cancellationToken) =>
        db.Bookings.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<SlotHold?> FindHoldAsync(SlotHoldId id, CancellationToken cancellationToken) =>
        db.SlotHolds.SingleOrDefaultAsync(h => h.Id == id, cancellationToken);

    public Task<BlockedTime?> FindBlockedAsync(BlockedTimeId id, CancellationToken cancellationToken) =>
        db.BlockedTimes.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public void Remove(SlotHold hold) => db.SlotHolds.Remove(hold);

    public void Remove(BlockedTime blocked) => db.BlockedTimes.Remove(blocked);

    public Task RemoveExpiredHoldsAsync(MasterId masterId, Instant now, CancellationToken cancellationToken) =>
        db.SlotHolds.Where(h => h.MasterId == masterId && h.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

    public async Task<bool> IsBusyAsync(MasterId masterId, Instant start, Instant end, Instant now, SlotHoldId? ignoreHold, CancellationToken cancellationToken)
    {
        if (await db.Bookings.AnyAsync(b => b.MasterId == masterId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) && b.Start < end && start < b.End, cancellationToken))
        {
            return true;
        }

        var holds = db.SlotHolds.Where(h => h.MasterId == masterId && h.ExpiresAt > now && h.Start < end && start < h.End);
        if (ignoreHold is { } ignored)
        {
            holds = holds.Where(h => h.Id != ignored);
        }

        return await holds.AnyAsync(cancellationToken)
            || await db.BlockedTimes.AnyAsync(b => b.MasterId == masterId && b.Start < end && start < b.End, cancellationToken);
    }

    public async Task<CalendarView> ListAsync(Instant from, Instant to, MasterId? masterId, CancellationToken cancellationToken)
    {
        var bookings = db.Bookings.AsNoTracking().Where(b => b.Start < to && from < b.End);
        var blocked = db.BlockedTimes.AsNoTracking().Where(b => b.Start < to && from < b.End);
        if (masterId is { } id)
        {
            bookings = bookings.Where(b => b.MasterId == id);
            blocked = blocked.Where(b => b.MasterId == id);
        }

        var list = await bookings.OrderBy(b => b.Start).ToListAsync(cancellationToken);

        var masterIds = list.Select(b => b.MasterId).Distinct().ToList();
        var serviceIds = list.Select(b => b.ServiceId).Distinct().ToList();
        var clientIds = list.Where(b => b.ClientId is not null).Select(b => b.ClientId!.Value).Distinct().ToList();

        var masterNames = await db.Masters.AsNoTracking().Where(m => masterIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.DisplayName, cancellationToken);
        var serviceNames = await db.Services.AsNoTracking().Where(s => serviceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
        var clientsById = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var views = list.Select(b =>
            {
                var client = b.ClientId is { } clientId ? clientsById.GetValueOrDefault(clientId) : null;
                return new BookingView(
                    b.Id.Value,
                    b.MasterId.Value,
                    masterNames.GetValueOrDefault(b.MasterId) ?? string.Empty,
                    b.ServiceId.Value,
                    serviceNames.GetValueOrDefault(b.ServiceId) ?? string.Empty,
                    b.ClientId?.Value,
                    client?.Name ?? b.GuestName ?? string.Empty,
                    client?.Phone.Value,
                    b.Start.ToDateTimeOffset(),
                    b.End.ToDateTimeOffset(),
                    b.Status.ToString(),
                    b.Source.ToString(),
                    b.Price,
                    b.FinalPrice,
                    b.Note);
            })
            .ToList();

        var blockedViews = (await blocked.OrderBy(b => b.Start).ToListAsync(cancellationToken))
            .Select(b => new BlockedTimeView(b.Id.Value, b.MasterId.Value, b.Start.ToDateTimeOffset(), b.End.ToDateTimeOffset(), b.Reason))
            .ToList();

        return new CalendarView(views, blockedViews);
    }
}
