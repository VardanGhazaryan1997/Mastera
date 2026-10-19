namespace Mastera.Application.Tenancy;

/// <summary>Who the current tenant is. <see cref="Current"/> is null on the platform site and for system work.</summary>
public interface ITenantContext
{
    CurrentTenant? Current { get; }
}
