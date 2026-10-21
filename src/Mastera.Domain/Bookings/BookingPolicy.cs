using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Domain.Bookings;

/// <summary>Task 5.6: how late a client may cancel or move a booking themselves.</summary>
public static class BookingPolicy
{
    public static readonly Duration DefaultCancellationWindow = Duration.FromHours(2);

    public static void EnsureClientCanChange(Booking booking, Instant now, Duration window)
    {
        ArgumentNullException.ThrowIfNull(booking);
        if (now > booking.Start - window)
        {
            throw new DomainException(
                "booking.change.too_late",
                $"Bookings can be changed online up to {window.TotalHours:0.#} hours before the start. Please call the salon.");
        }
    }
}
