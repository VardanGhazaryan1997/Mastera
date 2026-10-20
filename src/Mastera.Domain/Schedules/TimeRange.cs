using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Domain.Schedules;

/// <summary>A stretch of time within one day, e.g. 10:00–14:00. Start is inclusive, end exclusive.</summary>
public sealed record TimeRange
{
    public TimeRange(LocalTime start, LocalTime end)
    {
        if (end <= start)
        {
            throw new DomainException("schedule.range.invalid", $"{start:HH:mm} must be before {end:HH:mm}.");
        }

        Start = start;
        End = end;
    }

    public LocalTime Start { get; }

    public LocalTime End { get; }

    public Duration Length => Duration.FromNanoseconds(End.NanosecondOfDay - Start.NanosecondOfDay);

    public bool Overlaps(TimeRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && other.Start < End;
    }

    public bool Contains(TimeRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start <= other.Start && other.End <= End;
    }

    public override string ToString() => $"{Start:HH:mm}–{End:HH:mm}";
}
