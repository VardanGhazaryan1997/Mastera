namespace Mastera.Domain.Tenants;

/// <summary>Strongly typed tenant id, so a tenant id can never be passed where a master id is expected.</summary>
public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
