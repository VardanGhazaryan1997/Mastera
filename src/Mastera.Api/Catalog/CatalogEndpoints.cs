using Mastera.Api.Auth;
using Mastera.Api.Common;
using Mastera.Application.Catalog;

namespace Mastera.Api.Catalog;

public sealed record CategoryRequest(string? Name, int SortOrder = 0);

public sealed record ServiceRequest(string? Name, int DurationMinutes, decimal Price, Guid? CategoryId = null, string? Description = null)
{
    public ServiceInput ToInput() => new(Name, DurationMinutes, Price, CategoryId, Description);
}

/// <summary>Task 2.1: the salon's service menu. Everyone in the salon can read it; owners and admins change it.</summary>
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var read = app.MapGroup("/api").RequireAuthorization(Policies.SalonMember).WithTags("Catalog");
        var write = app.MapGroup("/api").RequireAuthorization(Policies.SalonAdmin).WithTags("Catalog");

        read.MapGet("/service-categories", async (ICatalogRepository catalog, CancellationToken ct) =>
            Results.Ok(await catalog.ListCategoriesAsync(ct)));

        write.MapPost("/service-categories", async (CategoryRequest request, CatalogHandler handler, CancellationToken ct) =>
            (await handler.CreateCategoryAsync(request.Name, request.SortOrder, ct))
                .ToCreated(c => $"/api/service-categories/{c.Id}"));

        write.MapPut("/service-categories/{id:guid}", async (Guid id, CategoryRequest request, CatalogHandler handler, CancellationToken ct) =>
            (await handler.UpdateCategoryAsync(id, request.Name, request.SortOrder, ct)).ToHttp());

        read.MapGet("/services", async (bool? includeArchived, ICatalogRepository catalog, CancellationToken ct) =>
            Results.Ok(await catalog.ListServicesAsync(includeArchived ?? false, ct)));

        write.MapPost("/services", async (ServiceRequest request, CatalogHandler handler, CancellationToken ct) =>
            (await handler.CreateServiceAsync(request.ToInput(), ct)).ToCreated(s => $"/api/services/{s.Id}"));

        write.MapPut("/services/{id:guid}", async (Guid id, ServiceRequest request, CatalogHandler handler, CancellationToken ct) =>
            (await handler.UpdateServiceAsync(id, request.ToInput(), ct)).ToHttp());

        write.MapPost("/services/{id:guid}/archive", async (Guid id, CatalogHandler handler, CancellationToken ct) =>
            (await handler.SetArchivedAsync(id, archived: true, ct)).ToHttp());

        write.MapPost("/services/{id:guid}/restore", async (Guid id, CatalogHandler handler, CancellationToken ct) =>
            (await handler.SetArchivedAsync(id, archived: false, ct)).ToHttp());

        return app;
    }
}
