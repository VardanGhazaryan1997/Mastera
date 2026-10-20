using Mastera.Application.Common;
using Mastera.Domain.Catalog;
using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Application.Catalog;

public sealed record ServiceInput(string? Name, int DurationMinutes, decimal Price, Guid? CategoryId, string? Description);

/// <summary>Task 2.1: service categories and services of the current salon.</summary>
public sealed class CatalogHandler(ICatalogRepository catalog, IUnitOfWork unitOfWork)
{
    public async Task<Result<ServiceCategoryView>> CreateCategoryAsync(string? name, int sortOrder, CancellationToken ct)
    {
        ServiceCategory category;
        try
        {
            category = ServiceCategory.Create(name, sortOrder);
        }
        catch (DomainException e)
        {
            return Result.Failure<ServiceCategoryView>(Errors.FromDomain(e));
        }

        if (await catalog.CategoryNameExistsAsync(category.Name, null, ct))
        {
            return Result.Failure<ServiceCategoryView>(DuplicateCategory(category.Name));
        }

        catalog.Add(category);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToView(category));
    }

    public async Task<Result<ServiceCategoryView>> UpdateCategoryAsync(Guid id, string? name, int sortOrder, CancellationToken ct)
    {
        var category = await catalog.FindCategoryAsync(new ServiceCategoryId(id), ct);
        if (category is null)
        {
            return Result.Failure<ServiceCategoryView>(CategoryNotFound());
        }

        try
        {
            category.Rename(name);
            category.MoveTo(sortOrder);
        }
        catch (DomainException e)
        {
            return Result.Failure<ServiceCategoryView>(Errors.FromDomain(e));
        }

        if (await catalog.CategoryNameExistsAsync(category.Name, category.Id, ct))
        {
            return Result.Failure<ServiceCategoryView>(DuplicateCategory(category.Name));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToView(category));
    }

    public async Task<Result<ServiceView>> CreateServiceAsync(ServiceInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var categoryError = await CheckCategoryAsync(input.CategoryId, ct);
        if (categoryError is not null)
        {
            return Result.Failure<ServiceView>(categoryError);
        }

        Service service;
        try
        {
            service = Service.Create(input.Name, Duration.FromMinutes(input.DurationMinutes), input.Price, ToCategoryId(input.CategoryId), input.Description);
        }
        catch (DomainException e)
        {
            return Result.Failure<ServiceView>(Errors.FromDomain(e));
        }

        if (await catalog.ServiceNameExistsAsync(service.Name, null, ct))
        {
            return Result.Failure<ServiceView>(DuplicateService(service.Name));
        }

        catalog.Add(service);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToView(service));
    }

    public async Task<Result<ServiceView>> UpdateServiceAsync(Guid id, ServiceInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var service = await catalog.FindServiceAsync(new ServiceId(id), ct);
        if (service is null)
        {
            return Result.Failure<ServiceView>(ServiceNotFound());
        }

        var categoryError = await CheckCategoryAsync(input.CategoryId, ct);
        if (categoryError is not null)
        {
            return Result.Failure<ServiceView>(categoryError);
        }

        try
        {
            service.Update(input.Name, Duration.FromMinutes(input.DurationMinutes), input.Price, ToCategoryId(input.CategoryId), input.Description);
        }
        catch (DomainException e)
        {
            return Result.Failure<ServiceView>(Errors.FromDomain(e));
        }

        if (await catalog.ServiceNameExistsAsync(service.Name, service.Id, ct))
        {
            return Result.Failure<ServiceView>(DuplicateService(service.Name));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToView(service));
    }

    public async Task<Result<ServiceView>> SetArchivedAsync(Guid id, bool archived, CancellationToken ct)
    {
        var service = await catalog.FindServiceAsync(new ServiceId(id), ct);
        if (service is null)
        {
            return Result.Failure<ServiceView>(ServiceNotFound());
        }

        if (archived)
        {
            service.Archive();
        }
        else
        {
            service.Restore();
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToView(service));
    }

    public static ServiceView ToView(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return new ServiceView(
            service.Id.Value,
            service.CategoryId?.Value,
            service.Name,
            service.Description,
            (int)service.Duration.TotalMinutes,
            service.Price,
            service.IsArchived);
    }

    private static ServiceCategoryView ToView(ServiceCategory category) =>
        new(category.Id.Value, category.Name, category.SortOrder);

    private static ServiceCategoryId? ToCategoryId(Guid? id) => id is { } value ? new ServiceCategoryId(value) : null;

    private async Task<Error?> CheckCategoryAsync(Guid? categoryId, CancellationToken ct) =>
        categoryId is { } id && await catalog.FindCategoryAsync(new ServiceCategoryId(id), ct) is null
            ? CategoryNotFound()
            : null;

    private static Error CategoryNotFound() => Error.NotFound("category.not_found", "The category was not found.");

    private static Error ServiceNotFound() => Error.NotFound("service.not_found", "The service was not found.");

    private static Error DuplicateCategory(string name) =>
        Error.Conflict("category.name.taken", $"There is already a category called '{name}'.");

    private static Error DuplicateService(string name) =>
        Error.Conflict("service.name.taken", $"There is already a service called '{name}'.");
}
