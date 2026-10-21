using Mastera.Domain.Bookings;
using Mastera.Domain.Clients;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Application.Notifications;

/// <summary>What the dashboard shows in the bell and as a toast (task 6.3/6.7).</summary>
public sealed record LiveNotification(
    string Kind,
    Guid BookingId,
    Guid MasterId,
    string MasterName,
    string ServiceName,
    string ClientName,
    DateTimeOffset Start,
    DateTimeOffset OccurredAt);

/// <summary>Task 6.3: pushes live notifications to signed-in dashboard users (front desk + the master).</summary>
public interface IRealtimeNotifier
{
    Task PublishAsync(TenantId tenantId, MasterId masterId, LiveNotification notification, CancellationToken cancellationToken);
}

public sealed record SmsMessage(PhoneNumber To, string Text);

/// <summary>Task 6.4: text messages to clients.</summary>
public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

/// <summary>Task 6.4: messages to a master's Telegram chat.</summary>
public interface ITelegramSender
{
    Task SendAsync(long chatId, string text, CancellationToken cancellationToken);
}

/// <summary>
/// Task 6.2: remembers which notifications were already sent, so a message delivered twice by the outbox
/// (e.g. after a crash between sending and marking it processed) does not text the client twice.
/// </summary>
public interface INotificationLog
{
    /// <summary>Records <paramref name="key"/>; false if it was already recorded (then don't send again).</summary>
    Task<bool> TryRecordAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Task 5.6: the client's "manage my booking" link.</summary>
public interface IBookingLinks
{
    string CreateManageToken(BookingId bookingId);

    /// <summary>The booking a token was made for, or null if it was tampered with.</summary>
    BookingId? ReadManageToken(string? token);

    string ManageUrl(TenantSlug salon, string token);
}

public enum ReminderKind
{
    DayBefore,
    TwoHoursBefore,
}

public sealed record PlannedReminder(ReminderKind Kind, Instant DueAt);

/// <summary>Task 6.6: reminders saved in the database and sent by the background worker.</summary>
public interface IReminderScheduler
{
    /// <summary>Replaces the booking's unsent reminders.</summary>
    Task ReplaceAsync(BookingId bookingId, IReadOnlyList<PlannedReminder> reminders, CancellationToken cancellationToken);

    /// <summary>Removes the booking's unsent reminders.</summary>
    Task CancelAsync(BookingId bookingId, CancellationToken cancellationToken);
}

/// <summary>Runs the reaction to a domain event (called by the outbox processor, inside the event's salon).</summary>
public interface IDomainEventHandler
{
    Task HandleAsync(IDomainEvent domainEvent, Guid messageId, CancellationToken cancellationToken);
}
