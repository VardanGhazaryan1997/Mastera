using Mastera.Domain.Common;
using Mastera.Domain.Tenants;

namespace Mastera.Domain.Catalog;

/// <summary>Groups services on the booking page, e.g. "Haircuts", "Nails", "Make-up".</summary>
public sealed class ServiceCategory : Entity<ServiceCategoryId>, ITenantOwned
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 60;

    private ServiceCategory(ServiceCategoryId id, string name, int sortOrder)
        : base(id)
    {
        Name = name;
        SortOrder = sortOrder;
    }

    private ServiceCategory()
        : base(default)
    {
        Name = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    public int SortOrder { get; private set; }

    public static ServiceCategory Create(string? name, int sortOrder = 0) =>
        new(ServiceCategoryId.New(), ValidName(name), sortOrder);

    public void Rename(string? name) => Name = ValidName(name);

    public void MoveTo(int sortOrder) => SortOrder = sortOrder;

    private static string ValidName(string? name) =>
        Text.Required(name, NameMinLength, NameMaxLength, "category.name.length", "The category name");
}
