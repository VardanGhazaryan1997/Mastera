using System.Diagnostics.CodeAnalysis;

namespace Mastera.Application.Tenancy;

public enum TenantHostKind
{
    /// <summary>The root domain itself (mastera.ge or www.mastera.ge): the platform site, no salon.</summary>
    Platform,

    /// <summary>A subdomain of the root domain: a salon address candidate.</summary>
    Subdomain,

    /// <summary>Any other host (an IP, localhost in tests, a custom domain later).</summary>
    Other,
}

/// <summary>Works out which salon a host name points to, e.g. barber-house.mastera.ge → "barber-house".</summary>
public readonly record struct TenantHost(TenantHostKind Kind, string? Subdomain)
{
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Host names are compared in lowercase.")]
    public static TenantHost Parse(string? host, string rootDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDomain);

        var name = (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        var root = rootDomain.Trim().TrimEnd('.').ToLowerInvariant();

        if (name.Length == 0)
        {
            return new TenantHost(TenantHostKind.Other, null);
        }

        if (name == root || name == "www." + root)
        {
            return new TenantHost(TenantHostKind.Platform, null);
        }

        var suffix = "." + root;
        if (!name.EndsWith(suffix, StringComparison.Ordinal))
        {
            return new TenantHost(TenantHostKind.Other, null);
        }

        return new TenantHost(TenantHostKind.Subdomain, name[..^suffix.Length]);
    }
}
