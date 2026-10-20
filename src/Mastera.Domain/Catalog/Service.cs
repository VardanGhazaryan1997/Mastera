using Mastera.Domain.Common;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Catalog;

/// <summary>Something a salon sells: a haircut, a manicure. Masters may override its price and duration.</summary>
public sealed class Service : Entity<ServiceId>, ITenantOwned
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const decimal MaxPrice = 100_000m;
    public const int DurationStepMinutes = 5;

    public static readonly Duration MinDuration = Duration.FromMinutes(5);
    public static readonly Duration MaxDuration = Duration.FromHours(8);

    private Service(ServiceId id)
        : base(id)
    {
        Name = string.Empty;
    }

    private Service()
        : base(default)
    {
        Name = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public ServiceCategoryId? CategoryId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    /// <summary>How long the service takes, in steps of 5 minutes.</summary>
    public Duration Duration { get; private set; }

    /// <summary>Price in the salon's currency.</summary>
    public decimal Price { get; private set; }

    /// <summary>Archived services stay on past bookings but can't be booked or assigned.</summary>
    public bool IsArchived { get; private set; }

    public static Service Create(string? name, Duration duration, decimal price, ServiceCategoryId? categoryId = null, string? description = null)
    {
        var service = new Service(ServiceId.New());
        service.Update(name, duration, price, categoryId, description);
        return service;
    }

    public void Update(string? name, Duration duration, decimal price, ServiceCategoryId? categoryId = null, string? description = null)
    {
        var validName = Text.Required(name, NameMinLength, NameMaxLength, "service.name.length", "The service name");
        var validDescription = Text.Optional(description, DescriptionMaxLength, "service.description.length", "The description");
        EnsureValidDuration(duration);
        EnsureValidPrice(price);

        Name = validName;
        Description = validDescription;
        Duration = duration;
        Price = price;
        CategoryId = categoryId;
    }

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;

    public static void EnsureValidDuration(Duration duration)
    {
        var minutes = duration.TotalMinutes;
        if (duration < MinDuration || duration > MaxDuration || minutes % DurationStepMinutes != 0)
        {
            throw new DomainException(
                "service.duration.invalid",
                $"The duration must be {MinDuration.TotalMinutes}–{MaxDuration.TotalMinutes} minutes, in steps of {DurationStepMinutes}.");
        }
    }

    public static void EnsureValidPrice(decimal price)
    {
        if (price < 0 || price > MaxPrice || decimal.Round(price, 2) != price)
        {
            throw new DomainException("service.price.invalid", $"The price must be between 0 and {MaxPrice}, with at most 2 decimals.");
        }
    }
}
