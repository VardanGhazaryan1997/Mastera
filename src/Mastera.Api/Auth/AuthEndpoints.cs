using System.Security.Claims;
using Mastera.Api.Common;
using Mastera.Application.Memberships;
using Mastera.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Mastera.Api.Auth;

public sealed record LoginRequest(string? Email, string? Password, bool RememberMe = false);

public sealed record MembershipResponse(Guid TenantId, string Slug, string Name, string Role);

public sealed record MeResponse(Guid UserId, string Email, string DisplayName, IReadOnlyList<MembershipResponse> Memberships);

public static class AuthEndpoints
{
    public const string InvalidCredentialsCode = "auth.invalid_credentials";
    public const string LockedOutCode = "auth.locked_out";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, SignInManager<ApplicationUser> signIn) =>
        {
            var result = await signIn.PasswordSignInAsync(
                (request.Email ?? string.Empty).Trim(),
                request.Password ?? string.Empty,
                isPersistent: request.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                return Results.NoContent();
            }

            return result.IsLockedOut
                ? ProblemResults.Problem(StatusCodes.Status401Unauthorized, "Too many attempts. Try again in 15 minutes.", LockedOutCode)
                : ProblemResults.Problem(StatusCodes.Status401Unauthorized, "Wrong email or password.", InvalidCredentialsCode);
        });

        group.MapPost("/logout", async (SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        });

        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,
            IMembershipRepository memberships,
            CancellationToken cancellationToken) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var salons = await memberships.ListForUserAsync(user.Id, cancellationToken);
            return Results.Ok(new MeResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                salons.Select(m => new MembershipResponse(m.TenantId.Value, m.Slug.Value, m.SalonName, m.Role.ToString())).ToList()));
        }).RequireAuthorization();

        return app;
    }
}
