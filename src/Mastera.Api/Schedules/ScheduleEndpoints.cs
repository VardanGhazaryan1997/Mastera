using Mastera.Api.Auth;
using Mastera.Api.Common;
using Mastera.Application.Masters;
using Mastera.Application.Schedules;
using Mastera.Application.Users;

namespace Mastera.Api.Schedules;

public sealed record PatternRequest(WorkPatternDto? Pattern);

/// <summary>Task 3.4: a master's regular pattern and exceptions. Masters may change their own days off.</summary>
public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/masters").RequireAuthorization(Policies.SalonMember).WithTags("Schedules");

        // The master profile linked to the signed-in user (masters use it for "My schedule").
        group.MapGet("/me", async (ICurrentUser user, IMasterRepository masters, CancellationToken ct) =>
            user.UserId is { } id && await masters.FindByUserAsync(id, ct) is { } master
                ? Results.Ok(MasterHandler.ToView(master))
                : ProblemResults.Problem(StatusCodes.Status404NotFound, "You have no master profile in this salon.", "master.not_found"));

        group.MapGet("/{id:guid}/schedule", async (Guid id, ScheduleHandler handler, CancellationToken ct) =>
            (await handler.GetAsync(id, ct)).ToHttp());

        group.MapPut("/{id:guid}/schedule/pattern", async (Guid id, PatternRequest request, ScheduleHandler handler, CancellationToken ct) =>
            (await handler.SetPatternAsync(id, request.Pattern, ct)).ToHttp());

        group.MapPost("/{id:guid}/schedule/exceptions", async (Guid id, ExceptionInput request, ScheduleHandler handler, CancellationToken ct) =>
            (await handler.AddExceptionAsync(id, request, ct)).ToCreated(e => $"/api/masters/{id}/schedule/exceptions/{e.Id}"));

        group.MapDelete("/{id:guid}/schedule/exceptions/{exceptionId:guid}", async (Guid id, Guid exceptionId, ScheduleHandler handler, CancellationToken ct) =>
        {
            var result = await handler.RemoveExceptionAsync(id, exceptionId, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        });

        return app;
    }
}
