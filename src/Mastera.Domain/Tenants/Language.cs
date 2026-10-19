using System.Diagnostics.CodeAnalysis;
using Mastera.Domain.Common;

namespace Mastera.Domain.Tenants;

/// <summary>A language the platform is translated into. Keep in sync with web/packages/i18n.</summary>
public sealed record Language
{
    public static readonly Language Georgian = new("ka");
    public static readonly Language Russian = new("ru");
    public static readonly Language English = new("en");

    public static readonly IReadOnlyList<Language> Supported = [Georgian, Russian, English];

    private Language(string code)
    {
        Code = code;
    }

    public string Code { get; }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Language tags are lowercase.")]
    public static Language Create(string? raw)
    {
        var code = (raw ?? string.Empty).Trim().ToLowerInvariant();

        return Supported.FirstOrDefault(l => l.Code == code)
            ?? throw new DomainException(
                "tenant.language.unsupported",
                $"'{code}' is not supported. Use one of: {string.Join(", ", Supported.Select(l => l.Code))}.");
    }

    public override string ToString() => Code;
}
