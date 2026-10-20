namespace Mastera.Application.Tenancy;

/// <summary>Lets a use case that starts without a salon (e.g. accepting an invitation) work inside one.</summary>
public interface ITenantSwitcher
{
    void Use(CurrentTenant tenant);
}
