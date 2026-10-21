using Mastera.Domain.Common;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;

namespace Mastera.Infrastructure.Outbox;

/// <summary>
/// Task 6.1: before saving, turns the domain events raised by tracked entities into outbox rows,
/// which are then saved in the same transaction as the entities.
/// </summary>
public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    public static readonly OutboxInterceptor Instance = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Collect(DbContext? context)
    {
        if (context is not MasteraDbContext db)
        {
            return;
        }

        var raisers = db.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.PeekDomainEvents().Count > 0)
            .ToList();
        if (raisers.Count == 0)
        {
            return;
        }

        var now = SystemClock.Instance.GetCurrentInstant();
        foreach (var entity in raisers)
        {
            foreach (var domainEvent in entity.PeekDomainEvents())
            {
                var (type, payload) = DomainEventSerializer.Serialize(domainEvent);
                db.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = db.CurrentTenantId.Value,
                    Type = type,
                    Payload = payload,
                    OccurredAt = now,
                });
            }

            entity.ClearDomainEvents();
        }
    }
}
