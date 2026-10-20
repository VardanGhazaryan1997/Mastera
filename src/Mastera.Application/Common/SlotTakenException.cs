namespace Mastera.Application.Common;

/// <summary>
/// The database refused a booking or hold because it overlaps another one for the same master
/// (someone was faster). Turned into a 409 "booking.slot_taken" for the client.
/// </summary>
public class SlotTakenException : Exception
{
    public SlotTakenException()
        : base("The time slot was taken by someone else.")
    {
    }

    public SlotTakenException(string message)
        : base(message)
    {
    }

    public SlotTakenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
