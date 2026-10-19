using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Mastera.Domain.Common;

namespace Mastera.Domain.Tenants;

/// <summary>
/// The salon's subdomain, e.g. "barber-house" in barber-house.mastera.ge.
/// Lowercase letters, digits and single hyphens; 3–40 characters; not a reserved name.
/// </summary>
public sealed partial record TenantSlug
{
    public const int MinLength = 3;
    public const int MaxLength = 40;

    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
    {
        "www", "api", "app", "admin", "dashboard", "booking", "book", "mail", "smtp", "static", "cdn",
        "assets", "help", "support", "status", "docs", "blog", "auth", "login", "account", "billing", "mastera",
    };

    private TenantSlug(string value)
    {
        Value = value;
    }

    public string Value { get; }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Subdomains are lowercase by convention.")]
    public static TenantSlug Create(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();

        if (value.Length is < MinLength or > MaxLength)
        {
            throw new DomainException(
                "tenant.slug.length",
                $"The address must be {MinLength}–{MaxLength} characters long.");
        }

        if (!Pattern().IsMatch(value))
        {
            throw new DomainException(
                "tenant.slug.format",
                "The address may contain only Latin letters, digits and single hyphens, and must start and end with a letter or digit.");
        }

        if (Reserved.Contains(value))
        {
            throw new DomainException("tenant.slug.reserved", $"The address '{value}' is reserved.");
        }

        return new TenantSlug(value);
    }

    /// <summary>Non-throwing variant for untrusted input such as a request's host name.</summary>
    public static bool TryCreate(string? raw, [NotNullWhen(true)] out TenantSlug? slug)
    {
        try
        {
            slug = Create(raw);
            return true;
        }
        catch (DomainException)
        {
            slug = null;
            return false;
        }
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
