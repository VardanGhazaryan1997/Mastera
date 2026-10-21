using System.Security.Cryptography;
using System.Text;
using Mastera.Application.Notifications;
using Mastera.Domain.Bookings;
using Mastera.Domain.Masters;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Mastera.Infrastructure.Outbox;

public sealed class NotificationLog(MasteraDbContext db, IClock clock) : INotificationLog
{
    public async Task<bool> TryRecordAsync(string key, CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "NotificationLog" ("Key", "CreatedAt") VALUES ({key}, {now}) ON CONFLICT ("Key") DO NOTHING""",
            cancellationToken);
        return inserted == 1;
    }
}

public sealed class ReminderScheduler(MasteraDbContext db) : IReminderScheduler
{
    public async Task ReplaceAsync(BookingId bookingId, IReadOnlyList<PlannedReminder> reminders, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        await CancelAsync(bookingId, cancellationToken);
        foreach (var reminder in reminders)
        {
            db.ScheduledReminders.Add(new ScheduledReminder
            {
                Id = Guid.NewGuid(),
                TenantId = db.CurrentTenantId.Value,
                BookingId = bookingId.Value,
                Kind = reminder.Kind.ToString(),
                DueAt = reminder.DueAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task CancelAsync(BookingId bookingId, CancellationToken cancellationToken) =>
        db.ScheduledReminders
            .Where(r => r.BookingId == bookingId.Value && r.SentAt == null)
            .ExecuteDeleteAsync(cancellationToken);
}

public sealed class TelegramLinkStore(MasteraDbContext db) : ITelegramLinkStore
{
    public async Task<string> CreateAsync(TenantId tenantId, MasterId masterId, Instant expiresAt, CancellationToken cancellationToken)
    {
        // Telegram's /start parameter allows A-Z, a-z, 0-9, _ and - (up to 64 characters).
        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_');
        db.TelegramLinkRequests.Add(new TelegramLinkRequest
        {
            CodeHash = Hash(code),
            TenantId = tenantId.Value,
            MasterId = masterId.Value,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(cancellationToken);
        return code;
    }

    public async Task<(TenantId TenantId, MasterId MasterId)?> RedeemAsync(string code, Instant now, CancellationToken cancellationToken)
    {
        var hash = Hash(code);
        var request = await db.TelegramLinkRequests.SingleOrDefaultAsync(r => r.CodeHash == hash, cancellationToken);
        if (request is null)
        {
            return null;
        }

        db.TelegramLinkRequests.Remove(request);
        await db.SaveChangesAsync(cancellationToken);
        return request.ExpiresAt > now ? (new TenantId(request.TenantId), new MasterId(request.MasterId)) : null;
    }

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
