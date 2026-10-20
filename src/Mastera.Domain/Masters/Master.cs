using Mastera.Domain.Catalog;
using Mastera.Domain.Common;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Masters;

/// <summary>A specialist clients book: barber, stylist, nail technician. May be linked to a user account.</summary>
public sealed class Master : Entity<MasterId>, ITenantOwned
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 60;
    public const int BioMaxLength = 500;

    private readonly List<MasterService> _services = [];

    private Master(MasterId id, string displayName, string? bio, Instant createdAt)
        : base(id)
    {
        DisplayName = displayName;
        Bio = bio;
        CreatedAt = createdAt;
        IsActive = true;
    }

    private Master()
        : base(default)
    {
        DisplayName = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public string DisplayName { get; private set; }

    public string? Bio { get; private set; }

    /// <summary>The user account of this master, once they accepted an invitation.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Inactive masters are hidden from booking but keep their history.</summary>
    public bool IsActive { get; private set; }

    public Instant CreatedAt { get; private init; }

    public IReadOnlyCollection<MasterService> Services => _services;

    public static Master Create(string? displayName, string? bio, Instant now) =>
        new(MasterId.New(), ValidName(displayName), ValidBio(bio), now);

    public void UpdateProfile(string? displayName, string? bio)
    {
        var name = ValidName(displayName);
        var validBio = ValidBio(bio);
        DisplayName = name;
        Bio = validBio;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void LinkUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("master.user.required", "A user is required.");
        }

        if (UserId is { } existing && existing != userId)
        {
            throw new DomainException("master.user.already_linked", "This master is already linked to another account.");
        }

        UserId = userId;
    }

    public bool Offers(ServiceId serviceId) => _services.Any(s => s.ServiceId == serviceId);

    /// <summary>Replaces the services this master offers. Overrides are checked against the same rules as the service itself.</summary>
    public void SetServices(IEnumerable<ServiceOffer> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);
        var list = offers.ToList();

        if (list.Select(o => o.Service.Id).Distinct().Count() != list.Count)
        {
            throw new DomainException("master.service.duplicate", "A service is listed more than once.");
        }

        foreach (var offer in list)
        {
            if (offer.Service.IsArchived && !Offers(offer.Service.Id))
            {
                throw new DomainException("master.service.archived", $"'{offer.Service.Name}' is archived and can't be assigned.");
            }

            if (offer.Price is { } price)
            {
                Service.EnsureValidPrice(price);
            }

            if (offer.Duration is { } duration)
            {
                Service.EnsureValidDuration(duration);
            }
        }

        _services.RemoveAll(existing => list.All(o => o.Service.Id != existing.ServiceId));

        foreach (var offer in list)
        {
            var existing = _services.Find(s => s.ServiceId == offer.Service.Id);
            if (existing is null)
            {
                _services.Add(new MasterService(Id, offer.Service.Id, offer.Price, offer.Duration));
            }
            else
            {
                existing.ChangeOverrides(offer.Price, offer.Duration);
            }
        }
    }

    private static string ValidName(string? name) =>
        Text.Required(name, DisplayNameMinLength, DisplayNameMaxLength, "master.name.length", "The master's name");

    private static string? ValidBio(string? bio) =>
        Text.Optional(bio, BioMaxLength, "master.bio.length", "The bio");
}

/// <summary>A service offered by a master, with optional price and duration overrides.</summary>
public sealed record ServiceOffer(Service Service, decimal? Price = null, Duration? Duration = null);
