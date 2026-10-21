using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Domain.Tenants;

/// <summary>A salon (or chain) using Mastera. Every other tenant-owned record points to one.</summary>
public sealed class Tenant : Entity<TenantId>
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;

    private Tenant(TenantId id, string name, TenantSlug slug, TimeZoneId timeZone, Currency currency, Language defaultLanguage, Instant createdAt)
        : base(id)
    {
        Name = name;
        Slug = slug;
        TimeZone = timeZone;
        Currency = currency;
        DefaultLanguage = defaultLanguage;
        CreatedAt = createdAt;
    }

    // For EF Core materialization only.
    private Tenant()
        : base(default)
    {
        Name = string.Empty;
        Slug = null!;
        TimeZone = null!;
        Currency = null!;
        DefaultLanguage = null!;
    }

    public string Name { get; private set; }

    public TenantSlug Slug { get; private set; }

    public TimeZoneId TimeZone { get; private set; }

    public Currency Currency { get; private set; }

    public Language DefaultLanguage { get; private set; }

    public Instant CreatedAt { get; private init; }

    /// <summary>Task 5.8: masters see client phone numbers masked (owners worry about masters taking clients with them).</summary>
    public bool HideClientPhoneFromMasters { get; private set; }

    /// <summary>Task 5.6: clients may cancel or move online up to this many hours before the start.</summary>
    public int CancellationWindowHours { get; private set; } = 2;

    public static Tenant Create(
        string? name,
        TenantSlug slug,
        TimeZoneId timeZone,
        Currency currency,
        Language defaultLanguage,
        Instant now)
    {
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(defaultLanguage);

        return new Tenant(TenantId.New(), NormalizeName(name), slug, timeZone, currency, defaultLanguage, now);
    }

    public void Rename(string? name) => Name = NormalizeName(name);

    public void ChangeBookingSettings(bool hideClientPhoneFromMasters, int cancellationWindowHours)
    {
        if (cancellationWindowHours is < 0 or > 168)
        {
            throw new DomainException("tenant.cancellation_window.invalid", "The cancellation window must be 0–168 hours.");
        }

        HideClientPhoneFromMasters = hideClientPhoneFromMasters;
        CancellationWindowHours = cancellationWindowHours;
    }

    public void ChangeRegionalSettings(TimeZoneId timeZone, Currency currency, Language defaultLanguage)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(defaultLanguage);

        TimeZone = timeZone;
        Currency = currency;
        DefaultLanguage = defaultLanguage;
    }

    private static string NormalizeName(string? name)
    {
        var value = (name ?? string.Empty).Trim();

        if (value.Length is < NameMinLength or > NameMaxLength)
        {
            throw new DomainException(
                "tenant.name.length",
                $"The salon name must be {NameMinLength}–{NameMaxLength} characters long.");
        }

        return value;
    }
}
