using Mastera.Domain.Masters;
using Mastera.Domain.Schedules;

namespace Mastera.Application.Schedules;

public interface IScheduleRepository
{
    /// <summary>Loads a master's schedule with its exceptions, or null if none was saved yet.</summary>
    Task<MasterSchedule?> FindAsync(MasterId masterId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MasterSchedule>> ListAsync(IReadOnlyCollection<MasterId> masterIds, CancellationToken cancellationToken);

    void Add(MasterSchedule schedule);
}
