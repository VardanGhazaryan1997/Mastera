using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Domain.Tenants;

/// <summary>An IANA time zone id (e.g. "Asia/Tbilisi") that the salon's schedules are written in.</summary>
public sealed record TimeZoneId
{
    public static readonly TimeZoneId Tbilisi = new("Asia/Tbilisi");

    private TimeZoneId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static TimeZoneId Create(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();

        if (value.Length == 0 || DateTimeZoneProviders.Tzdb.GetZoneOrNull(value) is null)
        {
            throw new DomainException("tenant.timezone.invalid", $"'{value}' is not a known time zone.");
        }

        return new TimeZoneId(value);
    }

    public DateTimeZone ToDateTimeZone() => DateTimeZoneProviders.Tzdb[Value];

    public override string ToString() => Value;
}
