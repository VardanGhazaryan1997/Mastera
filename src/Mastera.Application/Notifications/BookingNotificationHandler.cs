using Mastera.Application.Bookings;
using Mastera.Application.Catalog;
using Mastera.Application.Clients;
using Mastera.Application.Email;
using Mastera.Application.Masters;
using Mastera.Application.Tenancy;
using Mastera.Domain.Bookings;
using Mastera.Domain.Clients;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using NodaTime;

namespace Mastera.Application.Notifications;

/// <summary>
/// Tasks 6.3–6.6: what happens after a booking changes. Runs in the background (outbox processor) inside the
/// booking's salon: live dashboard notification, Telegram message to the master, SMS/email to the client,
/// and the 24 h / 2 h reminders.
/// Each message is sent at most once per outbox message (see <see cref="INotificationLog"/>).
/// </summary>
public sealed class BookingNotificationHandler(
    IBookingRepository bookings,
    IMasterRepository masters,
    ICatalogRepository catalog,
    IClientRepository clients,
    ITenantContext tenantContext,
    ITenantSettings tenantSettings,
    IRealtimeNotifier realtime,
    ISmsSender sms,
    ITelegramSender telegram,
    IEmailSender email,
    INotificationLog log,
    IBookingLinks links,
    IReminderScheduler reminders,
    IClock clock) : IDomainEventHandler
{
    public static readonly Duration DayBefore = Duration.FromHours(24);
    public static readonly Duration TwoHoursBefore = Duration.FromHours(2);

    public async Task HandleAsync(IDomainEvent domainEvent, Guid messageId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        (BookingId BookingId, string? Kind) target = domainEvent switch
        {
            BookingCreatedEvent e => (e.BookingId, NotificationKinds.Created),
            BookingConfirmedEvent e => (e.BookingId, NotificationKinds.Confirmed),
            BookingCancelledEvent e => (e.BookingId, NotificationKinds.Cancelled),
            BookingRescheduledEvent e => (e.BookingId, NotificationKinds.Rescheduled),
            _ => (default, null),
        };
        var (bookingId, kind) = target;

        if (kind is null || await LoadAsync(bookingId, cancellationToken) is not { } context)
        {
            return;
        }

        var booking = context.Booking;
        var key = messageId.ToString("N");

        // Reminders follow the booking: moved with it, removed when it is cancelled.
        if (kind == NotificationKinds.Cancelled)
        {
            await reminders.CancelAsync(booking.Id, cancellationToken);
        }
        else if (kind is NotificationKinds.Created or NotificationKinds.Rescheduled && booking.IsActive)
        {
            await reminders.ReplaceAsync(booking.Id, PlanReminders(booking.Start, clock.GetCurrentInstant()), cancellationToken);
        }

        // Live notification to the dashboard (front desk and the master).
        await realtime.PublishAsync(context.Salon.Id, booking.MasterId, new LiveNotification(
            kind,
            booking.Id.Value,
            booking.MasterId.Value,
            context.Master.DisplayName,
            context.ServiceName,
            context.ClientName,
            booking.Start.ToDateTimeOffset(),
            clock.GetCurrentInstant().ToDateTimeOffset()), cancellationToken);

        // Telegram to the master, if they connected it.
        if (context.Master.TelegramChatId is { } chatId && await log.TryRecordAsync($"{key}:telegram", cancellationToken))
        {
            await telegram.SendAsync(chatId, NotificationTexts.ForMaster(context.Settings.Language, kind, context.ClientName, context.ServiceName, context.When), cancellationToken);
        }

        // The client hears about their own online booking, a moved booking, or the salon cancelling.
        var clientText = kind switch
        {
            NotificationKinds.Created when booking.Source == BookingSource.Online =>
                NotificationTexts.Booked(context.Settings.Language, context.Salon.Name, context.ServiceName, context.When, context.ManageUrl),
            NotificationKinds.Rescheduled =>
                NotificationTexts.Moved(context.Settings.Language, context.Salon.Name, context.ServiceName, context.When, context.ManageUrl),
            NotificationKinds.Cancelled when booking.Status == BookingStatus.CancelledBySalon =>
                NotificationTexts.CancelledBySalon(context.Settings.Language, context.Salon.Name, context.ServiceName, context.When),
            _ => null,
        };

        if (clientText is not null)
        {
            await SendToClientAsync(context.Client, clientText, key, cancellationToken);
        }
    }

    /// <summary>Task 6.6: called by the reminder worker when a reminder is due.</summary>
    public async Task SendReminderAsync(BookingId bookingId, ReminderKind reminderKind, Guid reminderId, CancellationToken cancellationToken)
    {
        if (await LoadAsync(bookingId, cancellationToken) is not { } context || !context.Booking.IsActive || context.Client is null)
        {
            return;
        }

        var text = NotificationTexts.Reminder(context.Settings.Language, context.Salon.Name, context.ServiceName, context.When, context.ManageUrl);
        await SendToClientAsync(context.Client, text, $"reminder:{reminderId:N}:{reminderKind}", cancellationToken);
    }

    /// <summary>Reminders 24 h and 2 h before the start, skipping any that would already be in the past.</summary>
    public static IReadOnlyList<PlannedReminder> PlanReminders(Instant start, Instant now) =>
        new[] { (ReminderKind.DayBefore, start - DayBefore), (ReminderKind.TwoHoursBefore, start - TwoHoursBefore) }
            .Where(r => r.Item2 > now)
            .Select(r => new PlannedReminder(r.Item1, r.Item2))
            .ToList();

    private async Task SendToClientAsync(Client? client, string text, string key, CancellationToken ct)
    {
        if (client is null)
        {
            return;
        }

        if (await log.TryRecordAsync($"{key}:sms", ct))
        {
            await sms.SendAsync(new SmsMessage(client.Phone, text), ct);
        }

        if (client.Email is { } address && await log.TryRecordAsync($"{key}:email", ct))
        {
            await email.SendAsync(new EmailMessage(address, text.Split(':')[0], text), ct);
        }
    }

    private async Task<NotificationContext?> LoadAsync(BookingId bookingId, CancellationToken ct)
    {
        if (tenantContext.Current is not { } salon
            || await bookings.FindAsync(bookingId, ct) is not { } booking
            || await masters.FindAsync(booking.MasterId, ct) is not { } master)
        {
            return null;
        }

        var service = await catalog.FindServiceAsync(booking.ServiceId, ct);
        var client = booking.ClientId is { } clientId ? await clients.FindAsync(clientId, ct) : null;
        var zone = await tenantSettings.GetTimeZoneAsync(salon.Id, ct);
        var settings = await tenantSettings.GetBookingSettingsAsync(salon.Id, ct);

        return new NotificationContext(
            salon,
            booking,
            master,
            client,
            service?.Name ?? string.Empty,
            client?.Name ?? booking.GuestName ?? string.Empty,
            NotificationTexts.FormatWhen(booking.Start.InZone(zone)),
            links.ManageUrl(salon.Slug, links.CreateManageToken(booking.Id)),
            settings);
    }

    private sealed record NotificationContext(
        CurrentTenant Salon,
        Booking Booking,
        Master Master,
        Client? Client,
        string ServiceName,
        string ClientName,
        string When,
        string ManageUrl,
        BookingSettings Settings);
}
