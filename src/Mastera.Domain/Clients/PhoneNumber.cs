using Mastera.Domain.Common;

namespace Mastera.Domain.Clients;

/// <summary>
/// A phone number in international form (+995555123456), used to recognise returning clients.
/// Accepts how people type numbers: spaces, dashes, brackets, "00" instead of "+", and Georgian mobile numbers
/// without the country code (555 12 34 56).
/// </summary>
public sealed record PhoneNumber
{
    private const string GeorgiaPrefix = "+995";

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PhoneNumber Create(string? raw)
    {
        var compact = new string((raw ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '(' or ')' or '.')).ToArray());

        if (compact.StartsWith("00", StringComparison.Ordinal))
        {
            compact = "+" + compact[2..];
        }

        string normalized;
        if (compact.StartsWith('+'))
        {
            normalized = compact;
        }
        else if (compact.Length == 9 && compact[0] == '5')
        {
            normalized = GeorgiaPrefix + compact;
        }
        else if (compact.Length == 12 && compact.StartsWith("995", StringComparison.Ordinal))
        {
            normalized = "+" + compact;
        }
        else
        {
            normalized = string.Empty;
        }

        var digits = normalized.Length > 1 ? normalized[1..] : string.Empty;
        if (digits.Length is < 8 or > 15 || !digits.All(char.IsAsciiDigit) || digits[0] == '0')
        {
            throw new DomainException("client.phone.invalid", "Enter a phone number like +995 555 12 34 56.");
        }

        return new PhoneNumber(normalized);
    }

    public override string ToString() => Value;
}
