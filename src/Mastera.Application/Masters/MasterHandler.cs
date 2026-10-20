using Mastera.Application.Catalog;
using Mastera.Application.Common;
using Mastera.Domain.Catalog;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using NodaTime;

namespace Mastera.Application.Masters;

public sealed record MasterServiceInput(Guid ServiceId, decimal? Price, int? DurationMinutes);

/// <summary>Tasks 2.2 and 2.3: master profiles and the services (with own price/duration) they offer.</summary>
public sealed class MasterHandler(IMasterRepository masters, ICatalogRepository catalog, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result<MasterView>> CreateAsync(string? displayName, string? bio, CancellationToken ct)
    {
        Master master;
        try
        {
            master = Master.Create(displayName, bio, clock.GetCurrentInstant());
        }
        catch (DomainException e)
        {
            return Result.Failure<MasterView>(Errors.FromDomain(e));
        }

        masters.Add(master);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToView(master));
    }

    public async Task<Result<MasterView>> UpdateAsync(Guid id, string? displayName, string? bio, bool isActive, CancellationToken ct)
    {
        var master = await masters.FindAsync(new MasterId(id), ct);
        if (master is null)
        {
            return Result.Failure<MasterView>(NotFound());
        }

        try
        {
            master.UpdateProfile(displayName, bio);
        }
        catch (DomainException e)
        {
            return Result.Failure<MasterView>(Errors.FromDomain(e));
        }

        if (isActive)
        {
            master.Activate();
        }
        else
        {
            master.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToView(master));
    }

    public async Task<Result<MasterView>> SetServicesAsync(Guid id, IReadOnlyList<MasterServiceInput> services, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(services);

        var master = await masters.FindAsync(new MasterId(id), ct);
        if (master is null)
        {
            return Result.Failure<MasterView>(NotFound());
        }

        var ids = services.Select(s => new ServiceId(s.ServiceId)).Distinct().ToList();
        var found = (await catalog.FindServicesAsync(ids, ct)).ToDictionary(s => s.Id);
        if (found.Count != ids.Count)
        {
            return Result.Failure<MasterView>(Error.NotFound("service.not_found", "One of the services was not found."));
        }

        try
        {
            master.SetServices(services.Select(s => new ServiceOffer(
                found[new ServiceId(s.ServiceId)],
                s.Price,
                s.DurationMinutes is { } minutes ? Duration.FromMinutes(minutes) : null)));
        }
        catch (DomainException e)
        {
            return Result.Failure<MasterView>(Errors.FromDomain(e));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToView(master));
    }

    public static MasterView ToView(Master master)
    {
        ArgumentNullException.ThrowIfNull(master);
        return new MasterView(
            master.Id.Value,
            master.DisplayName,
            master.Bio,
            master.IsActive,
            master.UserId,
            master.Services
                .Select(s => new MasterServiceView(s.ServiceId.Value, s.PriceOverride, s.DurationOverride is { } d ? (int)d.TotalMinutes : null))
                .ToList());
    }

    private static Error NotFound() => Error.NotFound("master.not_found", "The master was not found.");
}
