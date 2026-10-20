using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Domain.Schedules;

/// <summary>
/// A repeating cycle of days counted from an anchor date, e.g. 2 days on / 2 days off — common in salons.
/// Each day of the cycle has its hours, or null for a day off.
/// </summary>
public sealed class RotatingWorkPattern : IWorkPattern
{
    public const int MaxCycleLength = 28;

    public RotatingWorkPattern(LocalDate anchor, IReadOnlyList<DaySchedule?> cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        if (cycle.Count is < 2 or > MaxCycleLength)
        {
            throw new DomainException("schedule.cycle.length", $"A shift cycle must be 2–{MaxCycleLength} days long.");
        }

        if (cycle.All(day => day is null))
        {
            throw new DomainException("schedule.cycle.no_working_day", "A shift cycle needs at least one working day.");
        }

        Anchor = anchor;
        Cycle = cycle.ToList();
    }

    /// <summary>The first day of a cycle.</summary>
    public LocalDate Anchor { get; }

    public IReadOnlyList<DaySchedule?> Cycle { get; }

    /// <summary>Builds the classic "N days on, M days off" pattern with the same hours on every working day.</summary>
    public static RotatingWorkPattern OnOff(LocalDate anchor, int daysOn, int daysOff, DaySchedule hours)
    {
        if (daysOn < 1 || daysOff < 1)
        {
            throw new DomainException("schedule.cycle.length", "Days on and days off must each be at least 1.");
        }

        var cycle = Enumerable.Repeat<DaySchedule?>(hours, daysOn).Concat(Enumerable.Repeat<DaySchedule?>(null, daysOff)).ToList();
        return new RotatingWorkPattern(anchor, cycle);
    }

    public DaySchedule? ScheduleOn(LocalDate date)
    {
        var days = Period.Between(Anchor, date, PeriodUnits.Days).Days;
        var index = ((days % Cycle.Count) + Cycle.Count) % Cycle.Count;
        return Cycle[index];
    }
}
