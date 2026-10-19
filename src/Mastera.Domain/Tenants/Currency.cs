using Mastera.Domain.Common;

namespace Mastera.Domain.Tenants;

/// <summary>ISO 4217 currency code the salon prices its services in (GEL, AMD, USD, ...).</summary>
public sealed record Currency
{
    public static readonly Currency Gel = new("GEL");

    private Currency(string code)
    {
        Code = code;
    }

    public string Code { get; }

    public static Currency Create(string? raw)
    {
        var code = (raw ?? string.Empty).Trim().ToUpperInvariant();

        if (code.Length != 3 || !code.All(c => c is >= 'A' and <= 'Z'))
        {
            throw new DomainException("tenant.currency.invalid", "The currency must be a three-letter ISO 4217 code, e.g. GEL.");
        }

        return new Currency(code);
    }

    public override string ToString() => Code;
}
