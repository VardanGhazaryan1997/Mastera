using NodaTime;

namespace Mastera.Domain.Availability;

/// <summary>Time a master is not free: a booking, a slot held during checkout, blocked time.</summary>
public readonly record struct BusyInterval(Instant Start, Instant End)
{
    public Duration Length => End - Start;
}

/// <summary>A bookable start time for one master. End = start + that master's duration for the service.</summary>
public readonly record struct Slot(Instant Start, Instant End);
