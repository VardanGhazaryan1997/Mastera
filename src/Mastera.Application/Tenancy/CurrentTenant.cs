using Mastera.Domain.Tenants;

namespace Mastera.Application.Tenancy;

/// <summary>The salon the current request (or background job) is working for.</summary>
public sealed record CurrentTenant(TenantId Id, TenantSlug Slug, string Name);
