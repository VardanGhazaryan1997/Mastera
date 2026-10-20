using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Domain.Availability;

/// <summary>A salon's booking rules.</summary>
public sealed record AvailabilityRules
{
    public AvailabilityRules(Duration slotStep, Duration minimumNotice, Duration bookingHorizon, Duration buffer)
    {
        if (slotStep < Duration.FromMinutes(5) || slotStep > Duration.FromHours(2) || slotStep.TotalMinutes % 5 != 0)
        {
            throw new DomainException("availability.step.invalid", "The slot step must be 5–120 minutes, in steps of 5.");
        }

        if (minimumNotice < Duration.Zero || bookingHorizon <= Duration.Zero || buffer < Duration.Zero || buffer > Duration.FromHours(2))
        {
            throw new DomainException("availability.rules.invalid", "Notice, horizon and buffer must be positive (buffer at most 2 hours).");
        }

        SlotStep = slotStep;
        MinimumNotice = minimumNotice;
        BookingHorizon = bookingHorizon;
        Buffer = buffer;
    }

    /// <summary>Slots start on this grid, counted from midnight: every 15 minutes gives 10:00, 10:15, ...</summary>
    public Duration SlotStep { get; }

    /// <summary>How long before the start a slot can still be booked online.</summary>
    public Duration MinimumNotice { get; }

    /// <summary>How far ahead clients can book.</summary>
    public Duration BookingHorizon { get; }

    /// <summary>Free time kept after every appointment (cleaning, preparing).</summary>
    public Duration Buffer { get; }

    public static AvailabilityRules Default { get; } =
        new(Duration.FromMinutes(15), Duration.FromHours(1), Duration.FromDays(60), Duration.Zero);
}
