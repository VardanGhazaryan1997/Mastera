namespace Mastera.Domain.Common;

/// <summary>Shared checks for text entered by people: trimmed, with length limits and a stable error code.</summary>
public static class Text
{
    public static string Required(string? value, int minLength, int maxLength, string code, string label)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length < minLength || trimmed.Length > maxLength)
        {
            throw new DomainException(code, $"{label} must be {minLength}–{maxLength} characters long.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, int maxLength, string code, string label)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(code, $"{label} must be at most {maxLength} characters long.");
        }

        return trimmed;
    }
}
