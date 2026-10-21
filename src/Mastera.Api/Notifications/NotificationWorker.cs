using System.Diagnostics.CodeAnalysis;
using Mastera.Infrastructure.Outbox;

namespace Mastera.Api.Notifications;

public sealed class NotificationWorkerOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Off in tests, which run the processors themselves.</summary>
    public bool WorkerEnabled { get; set; } = true;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);

    public int BatchSize { get; set; } = 50;
}

/// <summary>
/// Tasks 6.2/6.6: the background worker that handles the outbox and sends due reminders. Running it on every app
/// instance is safe: rows are locked with SKIP LOCKED. (Replaces the Quartz.NET idea from the plan: one table-driven
/// loop is simpler and needs no extra library or tables.)
/// </summary>
public sealed partial class NotificationWorker(
    OutboxDispatcher outbox,
    ReminderDispatcher reminders,
    Microsoft.Extensions.Options.IOptions<NotificationWorkerOptions> options,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The worker must keep running; errors are logged.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.WorkerEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(settings.PollInterval);
        do
        {
            try
            {
                while (await outbox.ProcessPendingAsync(settings.BatchSize, stoppingToken) == settings.BatchSize)
                {
                    // A full batch: there may be more waiting.
                }

                await reminders.SendDueAsync(settings.BatchSize, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification worker run failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
