namespace Mastera.Application.Tenancy;

/// <summary>Thrown when code tries to read or write another salon's data. Always a bug, never user error.</summary>
public class TenantIsolationException : Exception
{
    public TenantIsolationException()
        : base("Tenant isolation was violated.")
    {
    }

    public TenantIsolationException(string message)
        : base(message)
    {
    }

    public TenantIsolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
