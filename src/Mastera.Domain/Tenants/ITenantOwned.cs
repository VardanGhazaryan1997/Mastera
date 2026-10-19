namespace Mastera.Domain.Tenants;

/// <summary>
/// Marks an entity that belongs to exactly one salon. Persistence stamps <see cref="TenantId"/> on insert,
/// filters every query by the current tenant, and PostgreSQL row-level security enforces the same rule.
/// </summary>
public interface ITenantOwned
{
    TenantId TenantId { get; }
}
