namespace Mastera.Domain.Schedules;

public readonly record struct ScheduleExceptionId(Guid Value)
{
    public static ScheduleExceptionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
