using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Schedules;

/// <summary>When a master works: their regular pattern plus exceptions (days off, vacations, changed hours).</summary>
public sealed class MasterSchedule : ITenantOwned
{
    private readonly List<ScheduleException> _exceptions = [];

    private MasterSchedule(MasterId masterId)
    {
        MasterId = masterId;
    }

    private MasterSchedule()
    {
    }

    public TenantId TenantId { get; private set; }

    public MasterId MasterId { get; private set; }

    /// <summary>The regular pattern; null until the salon sets one (the master then never works).</summary>
    public IWorkPattern? Pattern { get; private set; }

    public IReadOnlyCollection<ScheduleException> Exceptions => _exceptions;

    public static MasterSchedule For(MasterId masterId)
    {
        if (masterId == default)
        {
            throw new DomainException("schedule.master.required", "A schedule needs a master.");
        }

        return new MasterSchedule(masterId);
    }

    public void SetPattern(IWorkPattern? pattern) => Pattern = pattern;

    public ScheduleException AddException(LocalDate from, LocalDate to, DaySchedule? hours, string? note = null)
    {
        if (_exceptions.Any(e => e.Overlaps(from, to)))
        {
            throw new DomainException("schedule.exception.overlap", "These dates overlap another exception. Change or remove that one first.");
        }

        var exception = new ScheduleException(MasterId, from, to, hours, note);
        _exceptions.Add(exception);
        return exception;
    }

    public bool RemoveException(ScheduleExceptionId id) => _exceptions.RemoveAll(e => e.Id == id) > 0;

    /// <summary>The hours the master works on a date: an exception if one covers it, otherwise the pattern.</summary>
    public DaySchedule? ScheduleOn(LocalDate date)
    {
        var exception = _exceptions.Find(e => e.Covers(date));
        return exception is not null ? exception.Hours : Pattern?.ScheduleOn(date);
    }
}
