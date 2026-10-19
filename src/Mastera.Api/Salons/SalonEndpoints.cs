using Mastera.Api.Auth;
using Mastera.Api.Common;
using Mastera.Application.Memberships;
using Mastera.Application.Salons;
using Mastera.Application.Tenancy;
using Mastera.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Mastera.Api.Salons;

public sealed record SignUpRequest(
    string? SalonName,
    string? Slug,
    string? OwnerName,
    string? Email,
    string? Password,
    string? TimeZone = null,
    string? Currency = null,
    string? Language = null);

public sealed record SignUpResponse(Guid TenantId, string Slug);

public sealed record MemberResponse(Guid UserId, string Email, string DisplayName, string Role);

public static class SalonEndpoints
{
    public static IEndpointRouteBuilder MapSalonEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Task 1.6: a new salon signs up. The owner is signed in straight away.
        app.MapPost("/api/signup", async (
            SignUpRequest request,
            RegisterSalonHandler handler,
            UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new RegisterSalonCommand(
                    request.SalonName,
                    request.Slug,
                    request.OwnerName,
                    request.Email,
                    request.Password,
                    request.TimeZone,
                    request.Currency,
                    request.Language),
                cancellationToken);

            if (!result.IsSuccess)
            {
                return result.Error!.ToProblem();
            }

            var salon = result.Value!;
            var owner = await users.FindByIdAsync(salon.OwnerUserId.ToString());
            if (owner is not null)
            {
                await signIn.SignInAsync(owner, isPersistent: false);
            }

            return Results.Created("/api/tenant", new SignUpResponse(salon.TenantId.Value, salon.Slug.Value));
        }).WithTags("Salons");

        // The people working in the current salon. Owners and admins only.
        app.MapGet("/api/members", async (
            ITenantContext tenants,
            IMembershipRepository memberships,
            CancellationToken cancellationToken) =>
        {
            var members = await memberships.ListMembersAsync(tenants.Current!.Id, cancellationToken);
            return Results.Ok(members.Select(m => new MemberResponse(m.UserId, m.Email, m.DisplayName, m.Role.ToString())).ToList());
        }).RequireAuthorization(Policies.SalonAdmin).WithTags("Salons");

        return app;
    }
}
