using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Schedules;

/// <summary>
/// A change to the regular pattern for a date range: a day off or vacation (no hours), or different hours
/// (e.g. working a Sunday). Wins over the pattern on every date it covers.
/// </summary>
public sealed class ScheduleException : Entity<ScheduleExceptionId>, ITenantOwned
{
    public const int MaxDays = 366;
    public const int NoteMaxLength = 200;

    internal ScheduleException(MasterId masterId, LocalDate from, LocalDate to, DaySchedule? hours, string? note)
        : base(ScheduleExceptionId.New())
    {
        if (to < from)
        {
            throw new DomainException("schedule.exception.range", "The end date must not be before the start date.");
        }

        if (Period.Between(from, to, PeriodUnits.Days).Days >= MaxDays)
        {
            throw new DomainException("schedule.exception.range", $"An exception can cover at most {MaxDays} days.");
        }

        MasterId = masterId;
        From = from;
        To = to;
        Hours = hours;
        Note = Text.Optional(note, NoteMaxLength, "schedule.exception.note", "The note");
    }

    private ScheduleException()
        : base(default)
    {
    }

    public TenantId TenantId { get; private set; }

    public MasterId MasterId { get; private set; }

    public LocalDate From { get; private set; }

    /// <summary>Last date covered (inclusive).</summary>
    public LocalDate To { get; private set; }

    /// <summary>The hours on those dates; null means not working (day off, vacation, sick leave).</summary>
    public DaySchedule? Hours { get; private set; }

    public string? Note { get; private set; }

    public bool IsDayOff => Hours is null;

    public bool Covers(LocalDate date) => From <= date && date <= To;

    public bool Overlaps(LocalDate from, LocalDate to) => From <= to && from <= To;
}
