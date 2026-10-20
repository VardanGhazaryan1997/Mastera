using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Schedules;
using NodaTime;

namespace Mastera.Domain.UnitTests.Schedules;

public sealed class RotatingWorkPatternTests
{
    private static readonly DaySchedule Shift = new(new TimeRange(new LocalTime(10, 0), new LocalTime(20, 0)));
    private static readonly LocalDate Nov1 = new(2026, 11, 1);

    [Fact]
    public void Two_on_two_off_repeats_from_the_anchor()
    {
        var pattern = RotatingWorkPattern.OnOff(Nov1, daysOn: 2, daysOff: 2, Shift);

        var working = Enumerable.Range(0, 8).Select(i => pattern.ScheduleOn(Nov1.PlusDays(i)) is not null).ToList();

        working.ShouldBe([true, true, false, false, true, true, false, false]);
    }

    [Fact]
    public void Dates_before_the_anchor_follow_the_same_cycle()
    {
        var pattern = RotatingWorkPattern.OnOff(Nov1, 2, 2, Shift);

        pattern.ScheduleOn(Nov1.PlusDays(-1)).ShouldBeNull();
        pattern.ScheduleOn(Nov1.PlusDays(-2)).ShouldBeNull();
        pattern.ScheduleOn(Nov1.PlusDays(-3)).ShouldNotBeNull();
        pattern.ScheduleOn(Nov1.PlusDays(-4)).ShouldNotBeNull();
    }

    [Fact]
    public void Each_day_of_the_cycle_can_have_its_own_hours()
    {
        var late = new DaySchedule(new TimeRange(new LocalTime(14, 0), new LocalTime(22, 0)));
        var pattern = new RotatingWorkPattern(Nov1, [Shift, late, null]);

        pattern.ScheduleOn(Nov1.PlusDays(1)).ShouldBe(late);
        pattern.ScheduleOn(Nov1.PlusDays(4)).ShouldBe(late);
    }

    [Fact]
    public void A_cycle_needs_two_to_28_days_and_a_working_day()
    {
        Should.Throw<DomainException>(() => new RotatingWorkPattern(Nov1, [Shift])).Code.ShouldBe("schedule.cycle.length");
        Should.Throw<DomainException>(() => new RotatingWorkPattern(Nov1, Enumerable.Repeat<DaySchedule?>(Shift, 29).ToList()))
            .Code.ShouldBe("schedule.cycle.length");
        Should.Throw<DomainException>(() => new RotatingWorkPattern(Nov1, [null, null])).Code.ShouldBe("schedule.cycle.no_working_day");
        Should.Throw<DomainException>(() => RotatingWorkPattern.OnOff(Nov1, 0, 2, Shift)).Code.ShouldBe("schedule.cycle.length");
    }
}

public sealed class MasterScheduleTests
{
    private static readonly DaySchedule NineToSix = new(new TimeRange(new LocalTime(9, 0), new LocalTime(18, 0)));
    private static readonly LocalDate Monday = new(2026, 11, 2);

    private static MasterSchedule WeekdaysNineToSix()
    {
        var schedule = MasterSchedule.For(MasterId.New());
        schedule.SetPattern(new WeeklyWorkPattern(new Dictionary<IsoDayOfWeek, DaySchedule>
        {
            [IsoDayOfWeek.Monday] = NineToSix,
            [IsoDayOfWeek.Tuesday] = NineToSix,
            [IsoDayOfWeek.Wednesday] = NineToSix,
            [IsoDayOfWeek.Thursday] = NineToSix,
            [IsoDayOfWeek.Friday] = NineToSix,
        }));
        return schedule;
    }

    [Fact]
    public void Without_a_pattern_the_master_never_works()
    {
        MasterSchedule.For(MasterId.New()).ScheduleOn(Monday).ShouldBeNull();
    }

    [Fact]
    public void The_pattern_applies_when_no_exception_covers_the_date()
    {
        WeekdaysNineToSix().ScheduleOn(Monday).ShouldBe(NineToSix);
    }

    [Fact]
    public void A_vacation_overrides_the_pattern_on_every_day_it_covers()
    {
        var schedule = WeekdaysNineToSix();

        schedule.AddException(Monday, Monday.PlusDays(4), hours: null, "Vacation");

        Enumerable.Range(0, 5).ShouldAllBe(i => schedule.ScheduleOn(Monday.PlusDays(i)) == null);
        schedule.ScheduleOn(Monday.PlusDays(7)).ShouldBe(NineToSix);
    }

    [Fact]
    public void Extra_hours_make_a_day_off_a_working_day()
    {
        var schedule = WeekdaysNineToSix();
        var sunday = Monday.PlusDays(6);
        var sundayHours = new DaySchedule(new TimeRange(new LocalTime(11, 0), new LocalTime(15, 0)));

        schedule.AddException(sunday, sunday, sundayHours);

        schedule.ScheduleOn(sunday).ShouldBe(sundayHours);
    }

    [Fact]
    public void Overlapping_exceptions_are_rejected()
    {
        var schedule = WeekdaysNineToSix();
        schedule.AddException(Monday, Monday.PlusDays(2), null);

        Should.Throw<DomainException>(() => schedule.AddException(Monday.PlusDays(2), Monday.PlusDays(3), null))
            .Code.ShouldBe("schedule.exception.overlap");
    }

    [Fact]
    public void An_exception_can_be_removed()
    {
        var schedule = WeekdaysNineToSix();
        var dayOff = schedule.AddException(Monday, Monday, null);

        schedule.RemoveException(dayOff.Id).ShouldBeTrue();

        schedule.ScheduleOn(Monday).ShouldBe(NineToSix);
    }

    [Fact]
    public void Exception_dates_must_be_in_order_and_at_most_a_year()
    {
        var schedule = WeekdaysNineToSix();

        Should.Throw<DomainException>(() => schedule.AddException(Monday, Monday.PlusDays(-1), null)).Code.ShouldBe("schedule.exception.range");
        Should.Throw<DomainException>(() => schedule.AddException(Monday, Monday.PlusDays(366), null)).Code.ShouldBe("schedule.exception.range");
    }
}
