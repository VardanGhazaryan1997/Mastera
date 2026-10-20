using Mastera.Domain.Common;
using Mastera.Domain.Schedules;
using NodaTime;
using NodaTime.Text;

namespace Mastera.Application.Schedules;

/// <summary>A time range as "HH:mm" strings, e.g. { "start": "10:00", "end": "19:00" }.</summary>
public sealed record TimeRangeDto(string? Start, string? End);

/// <summary>Hours on one day with optional breaks.</summary>
public sealed record DayScheduleDto(string? Start, string? End, IReadOnlyList<TimeRangeDto>? Breaks = null);

/// <summary>
/// A work pattern as JSON. Weekly: { "kind": "weekly", "days": { "Monday": {...}, ... } }.
/// Rotating: { "kind": "rotating", "anchor": "2026-11-01", "cycle": [ {...}, {...}, null, null ] }.
/// </summary>
public sealed record WorkPatternDto(
    string? Kind,
    IReadOnlyDictionary<string, DayScheduleDto>? Days = null,
    string? Anchor = null,
    IReadOnlyList<DayScheduleDto?>? Cycle = null)
{
    public const string Weekly = "weekly";
    public const string Rotating = "rotating";
}

public sealed record ScheduleExceptionDto(Guid Id, string From, string To, DayScheduleDto? Hours, string? Note);

public sealed record ScheduleView(Guid MasterId, WorkPatternDto? Pattern, IReadOnlyList<ScheduleExceptionDto> Exceptions);

/// <summary>Converts between the JSON shapes above and the domain. Invalid input becomes a DomainException.</summary>
public static class ScheduleMapping
{
    private static readonly LocalTimePattern TimePattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");

    public static LocalTime ParseTime(string? value)
    {
        var result = TimePattern.Parse(value ?? string.Empty);
        return result.Success
            ? result.Value
            : throw new DomainException("schedule.time.invalid", $"'{value}' is not a time like 09:30.");
    }

    public static LocalDate ParseDate(string? value)
    {
        var result = LocalDatePattern.Iso.Parse(value ?? string.Empty);
        return result.Success
            ? result.Value
            : throw new DomainException("schedule.date.invalid", $"'{value}' is not a date like 2026-11-01.");
    }

    public static string Format(LocalTime time) => TimePattern.Format(time);

    public static string Format(LocalDate date) => LocalDatePattern.Iso.Format(date);

    public static DaySchedule ToDomain(DayScheduleDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new DaySchedule(
            new TimeRange(ParseTime(dto.Start), ParseTime(dto.End)),
            (dto.Breaks ?? []).Select(b => new TimeRange(ParseTime(b.Start), ParseTime(b.End))));
    }

    public static DayScheduleDto ToDto(DaySchedule day)
    {
        ArgumentNullException.ThrowIfNull(day);
        return new DayScheduleDto(
            Format(day.Hours.Start),
            Format(day.Hours.End),
            day.Breaks.Select(b => new TimeRangeDto(Format(b.Start), Format(b.End))).ToList());
    }

    public static IWorkPattern ToDomain(WorkPatternDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        switch (dto.Kind)
        {
            case WorkPatternDto.Weekly:
                var days = new Dictionary<IsoDayOfWeek, DaySchedule>();
                foreach (var (name, day) in dto.Days ?? new Dictionary<string, DayScheduleDto>())
                {
                    if (!Enum.TryParse<IsoDayOfWeek>(name, ignoreCase: true, out var weekday) || weekday == IsoDayOfWeek.None)
                    {
                        throw new DomainException("schedule.day.invalid", $"'{name}' is not a day of the week.");
                    }

                    days[weekday] = ToDomain(day);
                }

                return new WeeklyWorkPattern(days);

            case WorkPatternDto.Rotating:
                return new RotatingWorkPattern(
                    ParseDate(dto.Anchor),
                    (dto.Cycle ?? []).Select(day => day is null ? null : ToDomain(day)).ToList());

            default:
                throw new DomainException("schedule.pattern.kind", "The pattern must be \"weekly\" or \"rotating\".");
        }
    }

    public static WorkPatternDto ToDto(IWorkPattern pattern) => pattern switch
    {
        WeeklyWorkPattern weekly => new WorkPatternDto(
            WorkPatternDto.Weekly,
            Days: weekly.Days
                .OrderBy(d => d.Key)
                .ToDictionary(d => d.Key.ToString(), d => ToDto(d.Value))),
        RotatingWorkPattern rotating => new WorkPatternDto(
            WorkPatternDto.Rotating,
            Anchor: Format(rotating.Anchor),
            Cycle: rotating.Cycle.Select(d => d is null ? null : ToDto(d)).ToList()),
        _ => throw new ArgumentException($"Unknown pattern type {pattern?.GetType().Name}.", nameof(pattern)),
    };

    public static ScheduleExceptionDto ToDto(ScheduleException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new ScheduleExceptionDto(
            exception.Id.Value,
            Format(exception.From),
            Format(exception.To),
            exception.Hours is null ? null : ToDto(exception.Hours),
            exception.Note);
    }

    public static ScheduleView ToView(MasterSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return new ScheduleView(
            schedule.MasterId.Value,
            schedule.Pattern is null ? null : ToDto(schedule.Pattern),
            schedule.Exceptions.OrderBy(e => e.From).Select(ToDto).ToList());
    }
}
