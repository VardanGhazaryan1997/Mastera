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
