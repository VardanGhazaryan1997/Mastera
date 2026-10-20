using System.Text.Json;
using Mastera.Application.Schedules;
using Mastera.Domain.Schedules;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

/// <summary>Stores work patterns and day schedules as JSON (jsonb), in the same shape the API uses.</summary>
public static class ScheduleJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string SerializePattern(IWorkPattern pattern) =>
        JsonSerializer.Serialize(ScheduleMapping.ToDto(pattern), Options);

    public static IWorkPattern DeserializePattern(string json) =>
        ScheduleMapping.ToDomain(JsonSerializer.Deserialize<WorkPatternDto>(json, Options)
            ?? throw new InvalidOperationException("Empty work pattern JSON."));

    public static string SerializeDay(DaySchedule day) =>
        JsonSerializer.Serialize(ScheduleMapping.ToDto(day), Options);

    public static DaySchedule DeserializeDay(string json) =>
        ScheduleMapping.ToDomain(JsonSerializer.Deserialize<DayScheduleDto>(json, Options)
            ?? throw new InvalidOperationException("Empty day schedule JSON."));

    // EF Core never passes null to a converter (nulls stay NULL in the column); the nullable type matches the property.
    public static ValueConverter<IWorkPattern?, string> PatternConverter { get; } =
        new(pattern => SerializePattern(pattern!), json => DeserializePattern(json));

    public static ValueComparer<IWorkPattern?> PatternComparer { get; } = new(
        (a, b) => KeyOf(a) == KeyOf(b),
        pattern => KeyOf(pattern).GetHashCode(StringComparison.Ordinal),
        pattern => pattern);

    public static ValueConverter<DaySchedule?, string> DayConverter { get; } =
        new(day => SerializeDay(day!), json => DeserializeDay(json));

    public static ValueComparer<DaySchedule?> DayComparer { get; } = new(
        (a, b) => KeyOf(a) == KeyOf(b),
        day => KeyOf(day).GetHashCode(StringComparison.Ordinal),
        day => day);

    private static string KeyOf(IWorkPattern? pattern) => pattern is null ? string.Empty : SerializePattern(pattern);

    private static string KeyOf(DaySchedule? day) => day is null ? string.Empty : SerializeDay(day);
}
