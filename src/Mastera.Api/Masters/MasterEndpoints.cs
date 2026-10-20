using Mastera.Api.Auth;
using Mastera.Api.Common;
using Mastera.Application.Masters;

namespace Mastera.Api.Masters;

public sealed record MasterRequest(string? DisplayName, string? Bio = null, bool IsActive = true);

public sealed record MasterServicesRequest(IReadOnlyList<MasterServiceInput>? Services);

/// <summary>Tasks 2.2 and 2.3: master profiles and the services each master offers.</summary>
public static class MasterEndpoints
{
    public static IEndpointRouteBuilder MapMasterEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var read = app.MapGroup("/api/masters").RequireAuthorization(Policies.SalonMember).WithTags("Masters");
        var write = app.MapGroup("/api/masters").RequireAuthorization(Policies.SalonAdmin).WithTags("Masters");

        read.MapGet(string.Empty, async (IMasterRepository masters, CancellationToken ct) =>
            Results.Ok(await masters.ListAsync(ct)));

        write.MapPost(string.Empty, async (MasterRequest request, MasterHandler handler, CancellationToken ct) =>
            (await handler.CreateAsync(request.DisplayName, request.Bio, ct)).ToCreated(m => $"/api/masters/{m.Id}"));

        write.MapPut("/{id:guid}", async (Guid id, MasterRequest request, MasterHandler handler, CancellationToken ct) =>
            (await handler.UpdateAsync(id, request.DisplayName, request.Bio, request.IsActive, ct)).ToHttp());

        write.MapPut("/{id:guid}/services", async (Guid id, MasterServicesRequest request, MasterHandler handler, CancellationToken ct) =>
            (await handler.SetServicesAsync(id, request.Services ?? [], ct)).ToHttp());

        return app;
    }
}
