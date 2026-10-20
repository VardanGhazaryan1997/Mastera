using Mastera.Domain.Availability;
using Mastera.Domain.Schedules;
using NodaTime;

namespace Mastera.Domain.UnitTests.Availability;

public sealed class AvailabilityCalculatorTests
{
    private static readonly DateTimeZone Tbilisi = DateTimeZoneProviders.Tzdb["Asia/Tbilisi"];
    private static readonly LocalDate Monday = new(2026, 11, 2);

    /// <summary>A week before the date, so notice and horizon don't get in the way unless a test wants them to.</summary>
    private static readonly Instant LongBefore = Instant.FromUtc(2026, 10, 26, 0, 0);

    private static readonly AvailabilityRules Every30 =
        new(Duration.FromMinutes(30), Duration.Zero, Duration.FromDays(60), Duration.Zero);

    private static LocalTime T(int hour, int minute = 0) => new(hour, minute);

    private static DaySchedule Day(int from, int to, params TimeRange[] breaks) => new(new TimeRange(T(from), T(to)), breaks);

    /// <summary>Local wall-clock time on the test date in Tbilisi (UTC+4).</summary>
    private static Instant At(int hour, int minute = 0) => Tbilisi.AtStrictly(Monday + T(hour, minute)).ToInstant();

    private static IReadOnlyList<LocalTime> Starts(IEnumerable<Slot> slots) =>
        slots.Select(s => s.Start.InZone(Tbilisi).TimeOfDay).ToList();

    private static IReadOnlyList<Slot> Get(
        DaySchedule? day,
        int minutes,
        IEnumerable<BusyInterval>? busy = null,
        AvailabilityRules? rules = null,
        Instant? now = null) =>
        AvailabilityCalculator.GetSlots(day, Monday, Tbilisi, Duration.FromMinutes(minutes), busy ?? [], rules ?? Every30, now ?? LongBefore);

    // 4.1 — base slots

    [Fact]
    public void Slots_fill_the_working_hours_on_the_step_grid()
    {
        Starts(Get(Day(10, 12), 30)).ShouldBe([T(10), T(10, 30), T(11), T(11, 30)]);
    }

    [Fact]
    public void A_slot_must_end_by_closing_time()
    {
        Starts(Get(Day(10, 12), 60)).ShouldBe([T(10), T(10, 30), T(11)]);
    }

    [Fact]
    public void A_service_longer_than_the_day_has_no_slots()
    {
        Get(Day(10, 12), 150).ShouldBeEmpty();
    }

    [Fact]
    public void A_day_off_has_no_slots()
    {
        Get(null, 30).ShouldBeEmpty();
    }

    [Fact]
    public void Slots_never_run_into_a_break()
    {
        var slots = Get(Day(10, 14, new TimeRange(T(12), T(13))), 60);

        Starts(slots).ShouldBe([T(10), T(10, 30), T(11), T(13)]);
    }

    [Fact]
    public void Hours_off_the_grid_start_at_the_next_grid_time()
    {
        Starts(Get(new DaySchedule(new TimeRange(T(10, 10), T(11, 30))), 30)).ShouldBe([T(10, 30), T(11)]);
    }

    [Fact]
    public void Each_slot_ends_after_the_service_duration()
    {
        var slot = Get(Day(10, 11), 45).ShouldHaveSingleItem();

        (slot.End - slot.Start).ShouldBe(Duration.FromMinutes(45));
    }

    // 4.2 — bookings, holds and buffers

    [Fact]
    public void Slots_overlapping_a_booking_are_removed()
    {
        var booking = new BusyInterval(At(11), At(12));

        Starts(Get(Day(10, 14), 60, [booking])).ShouldBe([T(10), T(12), T(12, 30), T(13)]);
    }

    [Fact]
    public void A_hold_blocks_like_a_booking()
    {
        var hold = new BusyInterval(At(10), At(10, 30));

        Starts(Get(Day(10, 11, []), 30, [hold])).ShouldBe([T(10, 30)]);
    }

    [Fact]
    public void A_buffer_keeps_free_time_after_each_appointment()
    {
        var rules = new AvailabilityRules(Duration.FromMinutes(15), Duration.Zero, Duration.FromDays(60), Duration.FromMinutes(15));
        var booking = new BusyInterval(At(11), At(12));

        var starts = Starts(Get(Day(10, 13), 45, [booking], rules));

        starts.ShouldBe([T(10), T(12, 15)]);
    }

    // 4.3 — minimum notice and booking horizon

    [Fact]
    public void Slots_too_soon_are_not_offered()
    {
        var rules = new AvailabilityRules(Duration.FromMinutes(30), Duration.FromHours(2), Duration.FromDays(60), Duration.Zero);
        var now = At(9, 10);

        Starts(Get(Day(10, 13), 30, rules: rules, now: now)).ShouldBe([T(11, 30), T(12), T(12, 30)]);
    }

    [Fact]
    public void Slots_beyond_the_booking_horizon_are_not_offered()
    {
        var rules = new AvailabilityRules(Duration.FromMinutes(30), Duration.Zero, Duration.FromDays(7), Duration.Zero);
        var now = At(11).Minus(Duration.FromDays(7));

        Starts(Get(Day(10, 13), 30, rules: rules, now: now)).ShouldBe([T(10), T(10, 30), T(11)]);
    }

    [Fact]
    public void Nothing_in_the_past_is_offered()
    {
        Get(Day(10, 13), 30, now: At(14)).ShouldBeEmpty();
    }

    // 4.4 — time zones

    [Fact]
    public void Slots_are_local_times_stored_as_UTC_instants()
    {
        var first = Get(Day(10, 11), 30)[0];

        first.Start.ShouldBe(Instant.FromUtc(2026, 11, 2, 6, 0));
    }

    [Fact]
    public void Clocks_moving_forward_skip_the_missing_hour()
    {
        var berlin = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
        var springForward = new LocalDate(2026, 3, 29);
        var rules = new AvailabilityRules(Duration.FromMinutes(60), Duration.Zero, Duration.FromDays(400), Duration.Zero);

        var slots = AvailabilityCalculator.GetSlots(
            Day(1, 5), springForward, berlin, Duration.FromMinutes(60), [], rules, Instant.FromUtc(2026, 1, 1, 0, 0));

        slots.Select(s => s.Start.InZone(berlin).TimeOfDay).ShouldBe([T(1), T(3), T(4)]);
        slots.ShouldAllBe(s => s.End - s.Start == Duration.FromHours(1));
    }

    [Fact]
    public void Rules_reject_odd_steps()
    {
        Should.Throw<Mastera.Domain.Common.DomainException>(() =>
            new AvailabilityRules(Duration.FromMinutes(7), Duration.Zero, Duration.FromDays(1), Duration.Zero));
    }

    // 4.5 — any master

    [Fact]
    public void Any_master_merges_start_times_without_duplicates()
    {
        var ana = Guid.NewGuid();
        var luka = Guid.NewGuid();

        var slots = AvailabilityCalculator.GetSlotsForAnyMaster(
            [
                new MasterAvailabilityInput(ana, Day(10, 11), Duration.FromMinutes(30), []),
                new MasterAvailabilityInput(luka, Day(10, 12), Duration.FromMinutes(30), []),
            ],
            Monday,
            Tbilisi,
            Every30,
            LongBefore);

        slots.Select(s => s.Start.InZone(Tbilisi).TimeOfDay).ShouldBe([T(10), T(10, 30), T(11), T(11, 30)]);
        slots[0].Masters.Select(m => m.MasterKey).ShouldBe([ana, luka]);
        slots[3].Masters.Select(m => m.MasterKey).ShouldBe([luka]);
    }

    [Fact]
    public void Any_master_prefers_the_least_booked_master()
    {
        var busy = Guid.NewGuid();
        var free = Guid.NewGuid();

        var slots = AvailabilityCalculator.GetSlotsForAnyMaster(
            [
                new MasterAvailabilityInput(busy, Day(10, 14), Duration.FromMinutes(30), [new BusyInterval(At(12), At(14))]),
                new MasterAvailabilityInput(free, Day(10, 14), Duration.FromMinutes(30), []),
            ],
            Monday,
            Tbilisi,
            Every30,
            LongBefore);

        slots[0].Preferred.MasterKey.ShouldBe(free);
    }

    [Fact]
    public void Any_master_keeps_each_masters_own_duration()
    {
        var quick = Guid.NewGuid();
        var slow = Guid.NewGuid();

        var slot = AvailabilityCalculator.GetSlotsForAnyMaster(
            [
                new MasterAvailabilityInput(quick, Day(10, 11), Duration.FromMinutes(30), []),
                new MasterAvailabilityInput(slow, Day(10, 11), Duration.FromMinutes(60), []),
            ],
            Monday,
            Tbilisi,
            Every30,
            LongBefore)[0];

        slot.Masters.Single(m => m.MasterKey == quick).End.ShouldBe(At(10, 30));
        slot.Masters.Single(m => m.MasterKey == slow).End.ShouldBe(At(11));
    }
}
