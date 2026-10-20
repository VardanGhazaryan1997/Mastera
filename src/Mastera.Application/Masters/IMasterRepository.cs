using Mastera.Domain.Catalog;
using Mastera.Domain.Masters;

namespace Mastera.Application.Masters;

public sealed record MasterServiceView(Guid ServiceId, decimal? Price, int? DurationMinutes);

public sealed record MasterView(
    Guid Id,
    string DisplayName,
    string? Bio,
    bool IsActive,
    Guid? UserId,
    IReadOnlyList<MasterServiceView> Services);

/// <summary>Masters of the current salon (filtered by tenant).</summary>
public interface IMasterRepository
{
    /// <summary>Loads a master with the services they offer.</summary>
    Task<Master?> FindAsync(MasterId id, CancellationToken cancellationToken);

    void Add(Master master);

    Task<IReadOnlyList<MasterView>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Masters (with their services) who offer a service.</summary>
    Task<IReadOnlyList<Master>> ListOfferingAsync(ServiceId serviceId, CancellationToken cancellationToken);

    /// <summary>The master linked to a user account, if any.</summary>
    Task<Master?> FindByUserAsync(Guid userId, CancellationToken cancellationToken);
}
