using Mastera.Domain.Common;

namespace Mastera.Domain.Schedules;

/// <summary>Working hours on one day, with breaks (lunch) inside them.</summary>
public sealed class DaySchedule
{
    public DaySchedule(TimeRange hours, IEnumerable<TimeRange>? breaks = null)
    {
        ArgumentNullException.ThrowIfNull(hours);
        var sorted = (breaks ?? []).OrderBy(b => b.Start).ToList();

        foreach (var b in sorted)
        {
            if (!hours.Contains(b))
            {
                throw new DomainException("schedule.break.outside_hours", $"The break {b} is outside working hours {hours}.");
            }
        }

        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i - 1].Overlaps(sorted[i]))
            {
                throw new DomainException("schedule.break.overlap", $"The breaks {sorted[i - 1]} and {sorted[i]} overlap.");
            }
        }

        Hours = hours;
        Breaks = sorted;
    }

    public TimeRange Hours { get; }

    public IReadOnlyList<TimeRange> Breaks { get; }

    /// <summary>The working hours with the breaks cut out, in order.</summary>
    public IReadOnlyList<TimeRange> WorkingRanges()
    {
        var ranges = new List<TimeRange>();
        var cursor = Hours.Start;

        foreach (var b in Breaks)
        {
            if (b.Start > cursor)
            {
                ranges.Add(new TimeRange(cursor, b.Start));
            }

            cursor = b.End;
        }

        if (cursor < Hours.End)
        {
            ranges.Add(new TimeRange(cursor, Hours.End));
        }

        return ranges;
    }
}
