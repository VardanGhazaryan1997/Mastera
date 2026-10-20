using Mastera.Domain.Schedules;
using NodaTime;

namespace Mastera.Domain.Availability;

/// <summary>
/// Epic 4: works out the free start times for a service on a date. Pure and deterministic — everything it needs
/// (schedule, busy time, rules, clock) is passed in, so it is tested without a database.
/// </summary>
public static class AvailabilityCalculator
{
    /// <summary>Free slots for one master on one date (in the salon's time zone).</summary>
    public static IReadOnlyList<Slot> GetSlots(
        DaySchedule? day,
        LocalDate date,
        DateTimeZone zone,
        Duration serviceDuration,
        IEnumerable<BusyInterval> busy,
        AvailabilityRules rules,
        Instant now)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(busy);
        ArgumentNullException.ThrowIfNull(rules);

        if (day is null || serviceDuration <= Duration.Zero)
        {
            return [];
        }

        var earliest = now + rules.MinimumNotice;
        var latest = now + rules.BookingHorizon;
        var blocked = busy.Select(b => new BusyInterval(b.Start, b.End + rules.Buffer)).ToList();
        var step = rules.SlotStep.ToInt64Nanoseconds();
        var slots = new List<Slot>();

        foreach (var range in day.WorkingRanges())
        {
            var rangeEnd = ToInstant(date, range.End, zone);

            // First grid time (counted from midnight) at or after the start of the range.
            var startNanos = range.Start.NanosecondOfDay;
            var first = (startNanos + step - 1) / step * step;

            for (var nanos = first; nanos < NodaConstants.NanosecondsPerDay; nanos += step)
            {
                var localTime = LocalTime.FromNanosecondsSinceMidnight(nanos);
                if (localTime >= range.End)
                {
                    break;
                }

                var mapping = zone.MapLocal(date + localTime);
                if (mapping.Count == 0)
                {
                    continue; // This local time doesn't exist (clocks moved forward).
                }

                var start = mapping.First().ToInstant();
                var end = start + serviceDuration;

                if (end > rangeEnd || start < earliest || start > latest)
                {
                    continue;
                }

                var withBuffer = end + rules.Buffer;
                if (blocked.Any(b => start < b.End && b.Start < withBuffer))
                {
                    continue;
                }

                slots.Add(new Slot(start, end));
            }
        }

        return slots.Distinct().OrderBy(s => s.Start).ToList();
    }

    /// <summary>
    /// Task 4.5, "any master": free start times across several masters. Each start lists the masters free then,
    /// with the preferred one (least booked that day, then by order given) first.
    /// </summary>
    public static IReadOnlyList<AnyMasterSlot> GetSlotsForAnyMaster(
        IEnumerable<MasterAvailabilityInput> masters,
        LocalDate date,
        DateTimeZone zone,
        AvailabilityRules rules,
        Instant now)
    {
        ArgumentNullException.ThrowIfNull(masters);

        var options = masters
            .Select((master, order) => new
            {
                master,
                order,
                load = master.Busy.Aggregate(Duration.Zero, (total, b) => total + b.Length),
                slots = GetSlots(master.Day, date, zone, master.ServiceDuration, master.Busy, rules, now),
            })
            .SelectMany(m => m.slots.Select(slot => new { m.master, m.order, m.load, slot }));

        return options
            .GroupBy(o => o.slot.Start)
            .OrderBy(g => g.Key)
            .Select(g => new AnyMasterSlot(
                g.Key,
                g.OrderBy(o => o.load).ThenBy(o => o.order)
                    .Select(o => new MasterOption(o.master.MasterKey, o.slot.End))
                    .ToList()))
            .ToList();
    }

    private static Instant ToInstant(LocalDate date, LocalTime time, DateTimeZone zone) =>
        zone.AtLeniently(date + time).ToInstant();
}

/// <summary>One master's input for "any master": their day, their duration for the service, their busy time.</summary>
public sealed record MasterAvailabilityInput(Guid MasterKey, DaySchedule? Day, Duration ServiceDuration, IReadOnlyList<BusyInterval> Busy);

/// <summary>A start time and the masters free then, preferred first.</summary>
public sealed record AnyMasterSlot(Instant Start, IReadOnlyList<MasterOption> Masters)
{
    public MasterOption Preferred => Masters[0];
}

public sealed record MasterOption(Guid MasterKey, Instant End);
