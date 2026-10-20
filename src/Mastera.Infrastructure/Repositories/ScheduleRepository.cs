using Mastera.Application.Schedules;
using Mastera.Domain.Masters;
using Mastera.Domain.Schedules;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Repositories;

public sealed class ScheduleRepository(MasteraDbContext db) : IScheduleRepository
{
    public Task<MasterSchedule?> FindAsync(MasterId masterId, CancellationToken cancellationToken) =>
        db.MasterSchedules.Include(s => s.Exceptions).SingleOrDefaultAsync(s => s.MasterId == masterId, cancellationToken);

    public async Task<IReadOnlyList<MasterSchedule>> ListAsync(IReadOnlyCollection<MasterId> masterIds, CancellationToken cancellationToken) =>
        await db.MasterSchedules.AsNoTracking()
            .Include(s => s.Exceptions)
            .Where(s => masterIds.Contains(s.MasterId))
            .ToListAsync(cancellationToken);

    public void Add(MasterSchedule schedule) => db.MasterSchedules.Add(schedule);
}
