using Mastera.Domain.Common;
using Mastera.Domain.Schedules;
using NodaTime;

namespace Mastera.Domain.UnitTests.Schedules;

public sealed class WorkPatternTests
{
    private static LocalTime T(int hour, int minute = 0) => new(hour, minute);

    private static TimeRange R(int fromHour, int toHour) => new(T(fromHour), T(toHour));

    [Fact]
    public void A_range_must_end_after_it_starts()
    {
        Should.Throw<DomainException>(() => new TimeRange(T(14), T(10))).Code.ShouldBe("schedule.range.invalid");
        Should.Throw<DomainException>(() => new TimeRange(T(10), T(10))).Code.ShouldBe("schedule.range.invalid");
    }

    [Fact]
    public void Ranges_that_only_touch_do_not_overlap()
    {
        R(10, 12).Overlaps(R(12, 14)).ShouldBeFalse();
        R(10, 13).Overlaps(R(12, 14)).ShouldBeTrue();
    }

    [Fact]
    public void Working_ranges_exclude_breaks()
    {
        var day = new DaySchedule(R(10, 19), [new TimeRange(T(13), T(14)), new TimeRange(T(16), T(16, 30))]);

        day.WorkingRanges().ShouldBe([R(10, 13), R(14, 16), new TimeRange(T(16, 30), T(19))]);
    }

    [Fact]
    public void Breaks_are_sorted_whatever_order_they_are_given_in()
    {
        var day = new DaySchedule(R(10, 19), [R(16, 17), R(12, 13)]);

        day.Breaks.Select(b => b.Start).ShouldBe([T(12), T(16)]);
    }

    [Fact]
    public void A_break_at_the_start_or_end_of_the_day_trims_the_hours()
    {
        var day = new DaySchedule(R(9, 18), [R(9, 10), R(17, 18)]);

        day.WorkingRanges().ShouldBe([R(10, 17)]);
    }

    [Fact]
    public void Overlapping_breaks_are_rejected()
    {
        Should.Throw<DomainException>(() => new DaySchedule(R(10, 19), [R(12, 14), new TimeRange(T(13, 30), T(15))]))
            .Code.ShouldBe("schedule.break.overlap");
    }

    [Fact]
    public void A_break_outside_working_hours_is_rejected()
    {
        Should.Throw<DomainException>(() => new DaySchedule(R(10, 19), [R(18, 20)]))
            .Code.ShouldBe("schedule.break.outside_hours");
    }

    [Fact]
    public void The_weekly_pattern_gives_working_ranges_per_date()
    {
        var pattern = new WeeklyWorkPattern(new Dictionary<IsoDayOfWeek, DaySchedule>
        {
            [IsoDayOfWeek.Monday] = new(R(10, 19), [R(13, 14)]),
            [IsoDayOfWeek.Saturday] = new(R(11, 16)),
        });

        var monday = new LocalDate(2026, 10, 19);
        var sunday = new LocalDate(2026, 10, 25);
        var saturday = new LocalDate(2026, 10, 24);

        pattern.WorkingRangesOn(monday).ShouldBe([R(10, 13), R(14, 19)]);
        pattern.WorkingTimeOn(monday).ShouldBe(Duration.FromHours(8));
        pattern.WorkingRangesOn(saturday).ShouldBe([R(11, 16)]);
        pattern.IsWorkingDay(sunday).ShouldBeFalse();
        pattern.WorkingRangesOn(sunday).ShouldBeEmpty();
    }

    [Fact]
    public void None_is_not_a_weekday()
    {
        Should.Throw<ArgumentException>(() => new WeeklyWorkPattern(new Dictionary<IsoDayOfWeek, DaySchedule>
        {
            [IsoDayOfWeek.None] = new(R(10, 19)),
        }));
    }
}
