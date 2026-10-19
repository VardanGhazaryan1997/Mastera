using Mastera.Application.Tenancy;
using Mastera.Domain.Tenants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Mastera.Api.Tenancy;

/// <summary>
/// Finds the salon from the request's subdomain (barber-house.mastera.ge → "barber-house").
/// Unknown or invalid salon addresses get a 404; the platform domain and other hosts continue without a tenant.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
{
    public const string NotFoundCode = "tenant.not_found";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantLookup lookup,
        HttpTenantContext tenantContext,
        IProblemDetailsService problemDetails)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(problemDetails);

        var host = TenantHost.Parse(context.Request.Host.Host, options.Value.RootDomain);

        if (host.Kind == TenantHostKind.Subdomain)
        {
            var tenant = TenantSlug.TryCreate(host.Subdomain, out var slug)
                ? await lookup.FindBySlugAsync(slug, context.RequestAborted)
                : null;

            if (tenant is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Salon not found",
                        Detail = $"There is no salon at '{context.Request.Host.Host}'.",
                        Extensions = { ["code"] = NotFoundCode },
                    },
                });
                return;
            }

            tenantContext.Set(tenant);
        }

        await next(context);
    }
}
