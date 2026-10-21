namespace Mastera.Domain.Common;

/// <summary>Something that happened in the domain that other parts of the system react to (notifications, reminders).</summary>
public interface IDomainEvent
{
}

/// <summary>An entity that records domain events. They are saved to the outbox in the same transaction as the entity.</summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> PeekDomainEvents();

    void ClearDomainEvents();
}
