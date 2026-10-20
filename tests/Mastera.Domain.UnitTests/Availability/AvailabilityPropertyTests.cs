using Mastera.Domain.Availability;
using Mastera.Domain.Schedules;
using NodaTime;

namespace Mastera.Domain.UnitTests.Availability;

/// <summary>
/// Task 4.7: properties that must hold for any schedule, bookings and rules. Each test runs many random cases
/// from a fixed seed, so failures are reproducible; the failing seed and case number are in the message.
/// </summary>
public sealed class AvailabilityPropertyTests
{
    private const int Cases = 500;

    private static readonly DateTimeZone Zone = DateTimeZoneProviders.Tzdb["Asia/Tbilisi"];
    private static readonly LocalDate Date = new(2026, 11, 2);

    private sealed record Case(
        DaySchedule Day,
        Duration ServiceDuration,
        IReadOnlyList<BusyInterval> Busy,
        AvailabilityRules Rules,
        Instant Now);

    private static Case Generate(Random random)
    {
        var open = random.Next(6, 12) * 60 + random.Next(0, 4) * 15;
        var close = open + random.Next(2, 12) * 60;
        var hours = new TimeRange(LocalTime.FromMinutesSinceMidnight(open), LocalTime.FromMinutesSinceMidnight(Math.Min(close, 23 * 60 + 45)));

        var breaks = new List<TimeRange>();
        if (random.Next(2) == 0)
        {
            var breakStart = open + 60 + random.Next(0, 4) * 15;
            var breakEnd = breakStart + 30 + random.Next(0, 3) * 15;
            var lunch = new TimeRange(LocalTime.FromMinutesSinceMidnight(breakStart), LocalTime.FromMinutesSinceMidnight(breakEnd));
            if (hours.Contains(lunch))
            {
                breaks.Add(lunch);
            }
        }

        var dayStart = Zone.AtStrictly(Date.AtMidnight()).ToInstant();
        var busy = Enumerable.Range(0, random.Next(0, 6))
            .Select(_ =>
            {
                var start = dayStart + Duration.FromMinutes(open + random.Next(0, close - open));
                return new BusyInterval(start, start + Duration.FromMinutes(15 * random.Next(1, 9)));
            })
            .ToList();

        var rules = new AvailabilityRules(
            Duration.FromMinutes(5 * random.Next(1, 13)),
            Duration.FromMinutes(15 * random.Next(0, 9)),
            Duration.FromDays(random.Next(1, 90)),
            Duration.FromMinutes(5 * random.Next(0, 7)));

        var now = dayStart + Duration.FromMinutes(random.Next(-3 * 24 * 60, 18 * 60));

        return new Case(new DaySchedule(hours, breaks), Duration.FromMinutes(5 * random.Next(1, 37)), busy, rules, now);
    }

    private static void ForAll(int seed, Action<Case, IReadOnlyList<Slot>> property)
    {
        var random = new Random(seed);
        for (var i = 0; i < Cases; i++)
        {
            var c = Generate(random);
            var slots = AvailabilityCalculator.GetSlots(c.Day, Date, Zone, c.ServiceDuration, c.Busy, c.Rules, c.Now);
            try
            {
                property(c, slots);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                throw new InvalidOperationException($"Property failed for seed {seed}, case {i}: {c}", e);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    public void No_slot_overlaps_busy_time_or_its_buffer(int seed) =>
        ForAll(seed, (c, slots) =>
        {
            foreach (var slot in slots)
            {
                foreach (var b in c.Busy)
                {
                    var overlaps = slot.Start < b.End + c.Rules.Buffer && b.Start < slot.End + c.Rules.Buffer;
                    overlaps.ShouldBeFalse($"{slot} overlaps {b}");
                }
            }
        });

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Every_slot_lies_inside_one_working_range(int seed) =>
        ForAll(seed, (c, slots) =>
        {
            var ranges = c.Day.WorkingRanges()
                .Select(r => (Start: Zone.AtStrictly(Date + r.Start).ToInstant(), End: Zone.AtStrictly(Date + r.End).ToInstant()))
                .ToList();
            foreach (var slot in slots)
            {
                ranges.Any(r => r.Start <= slot.Start && slot.End <= r.End).ShouldBeTrue($"{slot} is outside working hours");
            }
        });

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Slots_respect_notice_horizon_duration_grid_and_order(int seed) =>
        ForAll(seed, (c, slots) =>
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                (slot.End - slot.Start).ShouldBe(c.ServiceDuration);
                slot.Start.ShouldBeGreaterThanOrEqualTo(c.Now + c.Rules.MinimumNotice);
                slot.Start.ShouldBeLessThanOrEqualTo(c.Now + c.Rules.BookingHorizon);
                (slot.Start.InZone(Zone).TimeOfDay.NanosecondOfDay % c.Rules.SlotStep.ToInt64Nanoseconds()).ShouldBe(0);
                if (i > 0)
                {
                    slot.Start.ShouldBeGreaterThan(slots[i - 1].Start);
                }
            }
        });

    [Theory]
    [InlineData(7)]
    [InlineData(99)]
    public void Adding_a_booking_never_adds_slots(int seed)
    {
        var random = new Random(seed);
        for (var i = 0; i < Cases; i++)
        {
            var c = Generate(random);
            var before = AvailabilityCalculator.GetSlots(c.Day, Date, Zone, c.ServiceDuration, c.Busy, c.Rules, c.Now);
            var extra = c.Busy.Count > 0 ? c.Busy[0] : new BusyInterval(c.Now, c.Now + Duration.FromHours(1));
            var after = AvailabilityCalculator.GetSlots(c.Day, Date, Zone, c.ServiceDuration, [.. c.Busy, extra with { End = extra.End + Duration.FromMinutes(30) }], c.Rules, c.Now);

            after.ShouldBeSubsetOf(before, $"seed {seed}, case {i}");
        }
    }
}
