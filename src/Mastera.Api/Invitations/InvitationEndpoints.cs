using System.Security.Claims;
using Mastera.Api.Auth;
using Mastera.Api.Common;
using Mastera.Application.Invitations;
using Mastera.Domain.Memberships;
using Mastera.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Mastera.Api.Invitations;

public sealed record InviteRequest(string? Email, MembershipRole Role = MembershipRole.Master, Guid? MasterId = null);

public sealed record AcceptRequest(string? Token, string? DisplayName = null, string? Password = null);

public sealed record AcceptResponse(string Slug, bool AccountCreated);

/// <summary>Task 2.2: invite people by email; the link lets them join (creating an account if needed).</summary>
public static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/invitations").WithTags("Invitations");

        group.MapPost(string.Empty, async (InviteRequest request, InvitationHandler handler, CancellationToken ct) =>
        {
            var result = await handler.InviteAsync(new InviteInput(request.Email, request.Role, request.MasterId), ct);
            return result.IsSuccess ? Results.Accepted() : result.Error!.ToProblem();
        }).RequireAuthorization(Policies.SalonAdmin);

        // Anyone holding the link may see who invited them; the token is the secret.
        group.MapGet("/{token}", async (string token, InvitationHandler handler, CancellationToken ct) =>
            (await handler.GetAsync(token, ct)).ToHttp());

        group.MapPost("/accept", async (
            AcceptRequest request,
            ClaimsPrincipal principal,
            InvitationHandler handler,
            UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn,
            CancellationToken ct) =>
        {
            var signedIn = principal.Identity?.IsAuthenticated == true ? await users.GetUserAsync(principal) : null;

            var result = await handler.AcceptAsync(
                new AcceptInput(request.Token ?? string.Empty, signedIn?.Id, signedIn?.Email, request.DisplayName, request.Password),
                ct);
            if (!result.IsSuccess)
            {
                return result.Error!.ToProblem();
            }

            var accepted = result.Value!;
            if (accepted.AccountCreated && await users.FindByIdAsync(accepted.UserId.ToString()) is { } user)
            {
                await signIn.SignInAsync(user, isPersistent: false);
            }

            return Results.Ok(new AcceptResponse(accepted.Slug.Value, accepted.AccountCreated));
        });

        return app;
    }
}
