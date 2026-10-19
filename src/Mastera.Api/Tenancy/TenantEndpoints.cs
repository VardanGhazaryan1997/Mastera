using Mastera.Application.Tenancy;

namespace Mastera.Api.Tenancy;

public sealed record TenantResponse(Guid Id, string Slug, string Name);

public static class TenantEndpoints
{
    public const string NoTenantCode = "tenant.none";

    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The salon this address belongs to. Used by the booking site to load its branding.
        app.MapGet("/api/tenant", (ITenantContext tenants) =>
            tenants.Current is { } tenant
                ? Results.Ok(new TenantResponse(tenant.Id.Value, tenant.Slug.Value, tenant.Name))
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "No salon at this address",
                    extensions: new Dictionary<string, object?> { ["code"] = NoTenantCode }))
            .WithName("GetCurrentTenant");

        return app;
    }
}
