using Mastera.Domain.Catalog;

namespace Mastera.Application.Catalog;

public sealed record ServiceCategoryView(Guid Id, string Name, int SortOrder);

public sealed record ServiceView(
    Guid Id,
    Guid? CategoryId,
    string Name,
    string? Description,
    int DurationMinutes,
    decimal Price,
    bool IsArchived);

/// <summary>Services and categories of the current salon (filtered by tenant).</summary>
public interface ICatalogRepository
{
    Task<ServiceCategory?> FindCategoryAsync(ServiceCategoryId id, CancellationToken cancellationToken);

    Task<Service?> FindServiceAsync(ServiceId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Service>> FindServicesAsync(IReadOnlyCollection<ServiceId> ids, CancellationToken cancellationToken);

    Task<bool> CategoryNameExistsAsync(string name, ServiceCategoryId? except, CancellationToken cancellationToken);

    Task<bool> ServiceNameExistsAsync(string name, ServiceId? except, CancellationToken cancellationToken);

    void Add(ServiceCategory category);

    void Add(Service service);

    Task<IReadOnlyList<ServiceCategoryView>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceView>> ListServicesAsync(bool includeArchived, CancellationToken cancellationToken);
}
