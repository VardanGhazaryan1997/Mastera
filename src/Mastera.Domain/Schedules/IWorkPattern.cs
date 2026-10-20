using NodaTime;

namespace Mastera.Domain.Schedules;

/// <summary>A master's regular working pattern: weekly, or a rotating cycle such as 2 days on / 2 days off.</summary>
public interface IWorkPattern
{
    /// <summary>The working hours on a date, or null on a day off.</summary>
    DaySchedule? ScheduleOn(LocalDate date);
}
