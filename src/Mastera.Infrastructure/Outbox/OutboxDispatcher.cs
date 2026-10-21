using System.Diagnostics.CodeAnalysis;
using Mastera.Application.Notifications;
using Mastera.Application.Tenancy;
using Mastera.Domain.Bookings;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Mastera.Infrastructure.Outbox;

/// <summary>
/// Task 6.2: handles waiting outbox messages. Rows are locked with FOR UPDATE SKIP LOCKED, so several workers
/// (or app instances) never pick the same message. Each message is handled in its own scope inside its salon.
/// A failing message is retried later, up to <see cref="OutboxMessage.MaxAttempts"/> times.
/// </summary>
public sealed class OutboxDispatcher(IServiceScopeFactory scopes, IClock clock, ILogger<OutboxDispatcher> logger)
{
    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasteraDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var messages = await db.OutboxMessages
            .FromSql($"""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAt" IS NULL AND "Attempts" < {OutboxMessage.MaxAttempts}
                ORDER BY "OccurredAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            await HandleAsync(message, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "One bad message must not stop the others; it is retried.")]
    private async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            if (!await TenantScope.EnterAsync(scope.ServiceProvider, new TenantId(message.TenantId), cancellationToken))
            {
                message.ProcessedAt = clock.GetCurrentInstant();
                message.Error = "The salon no longer exists.";
                return;
            }

            var domainEvent = DomainEventSerializer.Deserialize(message.Type, message.Payload);
            await scope.ServiceProvider.GetRequiredService<IDomainEventHandler>().HandleAsync(domainEvent, message.Id, cancellationToken);
            message.ProcessedAt = clock.GetCurrentInstant();
            message.Error = null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            message.Attempts++;
            message.Error = e.Message.Length > 2000 ? e.Message[..2000] : e.Message;
            LogFailed(logger, message.Id, message.Type, message.Attempts, e);
        }
    }

    private static readonly Action<ILogger, Guid, string, int, Exception?> LogFailed =
        LoggerMessage.Define<Guid, string, int>(LogLevel.Warning, new EventId(6201, "OutboxFailed"), "Outbox message {MessageId} ({Type}) failed, attempt {Attempts}");
}

/// <summary>Task 6.6: sends reminders that are due, the same way the outbox processor handles messages.</summary>
public sealed class ReminderDispatcher(IServiceScopeFactory scopes, IClock clock, ILogger<ReminderDispatcher> logger)
{
    public async Task<int> SendDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasteraDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = clock.GetCurrentInstant();
        var due = await db.ScheduledReminders
            .FromSql($"""
                SELECT * FROM "ScheduledReminders"
                WHERE "SentAt" IS NULL AND "DueAt" <= {now} AND "Attempts" < {OutboxMessage.MaxAttempts}
                ORDER BY "DueAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var reminder in due)
        {
            await SendAsync(reminder, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return due.Count;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "One bad reminder must not stop the others; it is retried.")]
    private async Task SendAsync(ScheduledReminder reminder, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            if (await TenantScope.EnterAsync(scope.ServiceProvider, new TenantId(reminder.TenantId), cancellationToken))
            {
                var kind = Enum.Parse<ReminderKind>(reminder.Kind);
                await scope.ServiceProvider.GetRequiredService<BookingNotificationHandler>()
                    .SendReminderAsync(new BookingId(reminder.BookingId), kind, reminder.Id, cancellationToken);
            }

            reminder.SentAt = clock.GetCurrentInstant();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            reminder.Attempts++;
            LogFailed(logger, reminder.Id, reminder.Attempts, e);
        }
    }

    private static readonly Action<ILogger, Guid, int, Exception?> LogFailed =
        LoggerMessage.Define<Guid, int>(LogLevel.Warning, new EventId(6601, "ReminderFailed"), "Reminder {ReminderId} failed, attempt {Attempts}");
}

internal static class TenantScope
{
    /// <summary>Makes the scope work inside the salon; false if it doesn't exist any more.</summary>
    public static async Task<bool> EnterAsync(IServiceProvider services, TenantId tenantId, CancellationToken cancellationToken)
    {
        var tenant = await services.GetRequiredService<ITenantLookupById>().FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return false;
        }

        services.GetRequiredService<ITenantSwitcher>().Use(tenant);
        return true;
    }
}
