using System.Security.Claims;
using Mastera.Api.Auth;
using Mastera.Api.Common;
using Mastera.Application.Memberships;
using Mastera.Application.Tenancy;
using Mastera.Domain.Tenants;
using Microsoft.Extensions.Options;

namespace Mastera.Api.Tenancy;

/// <summary>
/// Works out which salon a request is for, and the signed-in user's role there.
/// <list type="bullet">
/// <item>Public booking site: from the subdomain (barber-house.mastera.ge → "barber-house").
/// Unknown or invalid salon addresses get a 404.</item>
/// <item>Dashboard: from the <c>X-Mastera-Tenant</c> header, only for salons the user belongs to (otherwise 403).</item>
/// </list>
/// When the user is a member of the resolved salon, their role is added to the request's user, so
/// role-based authorization policies work. Runs after authentication, before authorization.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
{
    public const string NotFoundCode = "tenant.not_found";
    public const string ForbiddenCode = "tenant.forbidden";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantLookup lookup,
        IMembershipRepository memberships,
        HttpTenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(memberships);
        ArgumentNullException.ThrowIfNull(tenantContext);

        var userId = GetUserId(context.User);
        var host = TenantHost.Parse(context.Request.Host.Host, options.Value.RootDomain);

        if (host.Kind == TenantHostKind.Subdomain)
        {
            var tenant = TenantSlug.TryCreate(host.Subdomain, out var slug)
                ? await lookup.FindBySlugAsync(slug, context.RequestAborted)
                : null;

            if (tenant is null)
            {
                await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Salon not found", NotFoundCode);
                return;
            }

            tenantContext.Set(tenant);

            if (userId is { } id && await memberships.FindAsync(id, tenant.Slug, context.RequestAborted) is { } membership)
            {
                AddMembershipClaims(context.User, membership);
            }
        }
        else if (userId is { } id && context.Request.Headers.TryGetValue(MasteraClaims.TenantHeader, out var header))
        {
            var membership = TenantSlug.TryCreate(header.ToString(), out var slug)
                ? await memberships.FindAsync(id, slug, context.RequestAborted)
                : null;

            if (membership is null)
            {
                await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "You don't work in this salon", ForbiddenCode);
                return;
            }

            tenantContext.Set(new CurrentTenant(membership.TenantId, membership.Slug, membership.SalonName));
            AddMembershipClaims(context.User, membership);
        }

        await next(context);
    }

    private static Guid? GetUserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    private static void AddMembershipClaims(ClaimsPrincipal user, MembershipView membership) =>
        user.AddIdentity(new ClaimsIdentity(
        [
            new Claim(MasteraClaims.TenantId, membership.TenantId.Value.ToString()),
            new Claim(ClaimTypes.Role, membership.Role.ToString()),
        ]));

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string code)
    {
        // Problem results are written directly: the endpoint never runs.
        await ProblemResults.Problem(status, title, code).ExecuteAsync(context);
    }
}
