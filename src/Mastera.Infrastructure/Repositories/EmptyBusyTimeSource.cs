using Mastera.Application.Availability;
using Mastera.Domain.Availability;
using Mastera.Domain.Masters;
using NodaTime;

namespace Mastera.Infrastructure.Repositories;

/// <summary>Until bookings exist (Epic 5) nobody is busy. Replaced by a bookings-based source in task 5.2.</summary>
public sealed class EmptyBusyTimeSource : IBusyTimeSource
{
    public Task<IReadOnlyDictionary<MasterId, IReadOnlyList<BusyInterval>>> GetBusyAsync(
        IReadOnlyCollection<MasterId> masterIds, Instant from, Instant to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<MasterId, IReadOnlyList<BusyInterval>>>(
            new Dictionary<MasterId, IReadOnlyList<BusyInterval>>());
}
