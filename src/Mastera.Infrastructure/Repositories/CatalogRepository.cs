using Mastera.Application.Catalog;
using Mastera.Domain.Catalog;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Repositories;

public sealed class CatalogRepository(MasteraDbContext db) : ICatalogRepository
{
    private const string EscapeCharacter = "\\";

    public Task<ServiceCategory?> FindCategoryAsync(ServiceCategoryId id, CancellationToken cancellationToken) =>
        db.ServiceCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Service?> FindServiceAsync(ServiceId id, CancellationToken cancellationToken) =>
        db.Services.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Service>> FindServicesAsync(IReadOnlyCollection<ServiceId> ids, CancellationToken cancellationToken) =>
        await db.Services.Where(s => ids.Contains(s.Id)).ToListAsync(cancellationToken);

    public Task<bool> CategoryNameExistsAsync(string name, ServiceCategoryId? except, CancellationToken cancellationToken)
    {
        var pattern = ExactCaseInsensitive(name);
        var query = db.ServiceCategories.Where(c => EF.Functions.ILike(c.Name, pattern, EscapeCharacter));
        if (except is { } id)
        {
            query = query.Where(c => c.Id != id);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> ServiceNameExistsAsync(string name, ServiceId? except, CancellationToken cancellationToken)
    {
        var pattern = ExactCaseInsensitive(name);
        var query = db.Services.Where(s => EF.Functions.ILike(s.Name, pattern, EscapeCharacter));
        if (except is { } id)
        {
            query = query.Where(s => s.Id != id);
        }

        return query.AnyAsync(cancellationToken);
    }

    public void Add(ServiceCategory category) => db.ServiceCategories.Add(category);

    public void Add(Service service) => db.Services.Add(service);

    public async Task<IReadOnlyList<ServiceCategoryView>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await db.ServiceCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new ServiceCategoryView(c.Id.Value, c.Name, c.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ServiceView>> ListServicesAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var services = await db.Services.AsNoTracking()
            .Where(s => includeArchived || !s.IsArchived)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
        return services.Select(CatalogHandler.ToView).ToList();
    }

    /// <summary>An ILIKE pattern that matches the name exactly, ignoring case (%, _ and \ are escaped).</summary>
    private static string ExactCaseInsensitive(string name) =>
        name.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
