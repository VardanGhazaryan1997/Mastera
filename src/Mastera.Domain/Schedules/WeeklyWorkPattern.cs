using NodaTime;

namespace Mastera.Domain.Schedules;

/// <summary>
/// A master's regular week: working hours (with breaks) per weekday. Days that aren't listed are days off.
/// See <see cref="RotatingWorkPattern"/> for shift cycles and <see cref="MasterSchedule"/> for exceptions.
/// </summary>
public sealed class WeeklyWorkPattern : IWorkPattern
{
    private readonly Dictionary<IsoDayOfWeek, DaySchedule> _days;

    public WeeklyWorkPattern(IReadOnlyDictionary<IsoDayOfWeek, DaySchedule> days)
    {
        ArgumentNullException.ThrowIfNull(days);
        if (days.Keys.Any(d => d == IsoDayOfWeek.None))
        {
            throw new ArgumentException("IsoDayOfWeek.None is not a day.", nameof(days));
        }

        _days = new Dictionary<IsoDayOfWeek, DaySchedule>(days);
    }

    public IReadOnlyDictionary<IsoDayOfWeek, DaySchedule> Days => _days;

    public DaySchedule? For(IsoDayOfWeek day) => _days.GetValueOrDefault(day);

    public DaySchedule? ScheduleOn(LocalDate date) => For(date.DayOfWeek);

    public bool IsWorkingDay(LocalDate date) => _days.ContainsKey(date.DayOfWeek);

    /// <summary>The times the master works on a date (in the salon's local time), breaks excluded.</summary>
    public IReadOnlyList<TimeRange> WorkingRangesOn(LocalDate date) =>
        For(date.DayOfWeek)?.WorkingRanges() ?? [];

    public Duration WorkingTimeOn(LocalDate date) =>
        WorkingRangesOn(date).Aggregate(Duration.Zero, (total, range) => total + range.Length);
}
