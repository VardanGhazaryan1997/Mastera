namespace Mastera.Domain.Catalog;

public readonly record struct ServiceCategoryId(Guid Value)
{
    public static ServiceCategoryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
