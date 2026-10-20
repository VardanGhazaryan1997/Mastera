using Mastera.Application.Masters;
using Mastera.Domain.Catalog;
using Mastera.Domain.Masters;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Repositories;

public sealed class MasterRepository(MasteraDbContext db) : IMasterRepository
{
    public Task<Master?> FindAsync(MasterId id, CancellationToken cancellationToken) =>
        db.Masters.Include(m => m.Services).SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

    public void Add(Master master) => db.Masters.Add(master);

    public async Task<IReadOnlyList<MasterView>> ListAsync(CancellationToken cancellationToken)
    {
        var masters = await db.Masters.AsNoTracking()
            .Include(m => m.Services)
            .OrderBy(m => m.DisplayName)
            .ToListAsync(cancellationToken);
        return masters.Select(MasterHandler.ToView).ToList();
    }

    public async Task<IReadOnlyList<Master>> ListOfferingAsync(ServiceId serviceId, CancellationToken cancellationToken) =>
        await db.Masters.AsNoTracking()
            .Include(m => m.Services)
            .Where(m => m.Services.Any(s => s.ServiceId == serviceId))
            .OrderBy(m => m.DisplayName)
            .ToListAsync(cancellationToken);

    public Task<Master?> FindByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Masters.Include(m => m.Services).SingleOrDefaultAsync(m => m.UserId == userId, cancellationToken);
}
