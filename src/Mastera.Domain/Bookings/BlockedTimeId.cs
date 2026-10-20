namespace Mastera.Domain.Bookings;

public readonly record struct BlockedTimeId(Guid Value)
{
    public static BlockedTimeId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
