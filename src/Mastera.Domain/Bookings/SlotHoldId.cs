namespace Mastera.Domain.Bookings;

public readonly record struct SlotHoldId(Guid Value)
{
    public static SlotHoldId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
