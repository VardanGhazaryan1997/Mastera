namespace Mastera.Domain.Common;

/// <summary>
/// Thrown when a domain rule is broken. <see cref="Code"/> is a stable, machine-readable key
/// (e.g. "tenant.slug.invalid") that the API maps to a ProblemDetails response and the UI translates.
/// </summary>
public class DomainException : Exception
{
    public const string DefaultCode = "domain.error";

    public DomainException()
        : this(DefaultCode, "A domain rule was broken.")
    {
    }

    public DomainException(string message)
        : this(DefaultCode, message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = DefaultCode;
    }

    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
