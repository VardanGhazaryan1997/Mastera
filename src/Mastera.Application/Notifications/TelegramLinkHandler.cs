using Mastera.Application.Common;
using Mastera.Application.Masters;
using Mastera.Application.Tenancy;
using Mastera.Application.Users;
using Mastera.Domain.Masters;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Application.Notifications;

/// <summary>Task 6.5: a one-time code that ties a master to the Telegram chat that sends it to the bot.</summary>
public interface ITelegramLinkStore
{
    /// <summary>Saves a new code (only its hash) and returns it.</summary>
    Task<string> CreateAsync(TenantId tenantId, MasterId masterId, Instant expiresAt, CancellationToken cancellationToken);

    /// <summary>Uses up a code: returns who it was for, or null if it is unknown or expired.</summary>
    Task<(TenantId TenantId, MasterId MasterId)?> RedeemAsync(string code, Instant now, CancellationToken cancellationToken);
}

public sealed record TelegramLinkView(string Code, DateTimeOffset ExpiresAt, bool Linked);

/// <summary>Task 6.5: masters press "Connect Telegram", open the bot, and the bot's /start message links the chat.</summary>
public sealed class TelegramLinkHandler(
    ICurrentUser currentUser,
    IMasterRepository masters,
    ITelegramLinkStore links,
    ITenantLookupById tenants,
    ITenantSwitcher tenantSwitcher,
    ITenantSettings tenantSettings,
    ITelegramSender telegram,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public static readonly Duration CodeLifetime = Duration.FromMinutes(30);

    public async Task<Result<TelegramLinkView>> CreateCodeAsync(CancellationToken ct)
    {
        var master = currentUser.UserId is { } userId ? await masters.FindByUserAsync(userId, ct) : null;
        if (master is null)
        {
            return Result.Failure<TelegramLinkView>(Error.NotFound("telegram.no_master", "Only masters can connect Telegram."));
        }

        var expires = clock.GetCurrentInstant() + CodeLifetime;
        var code = await links.CreateAsync(master.TenantId, master.Id, expires, ct);
        return Result.Success(new TelegramLinkView(code, expires.ToDateTimeOffset(), master.TelegramChatId is not null));
    }

    public async Task<Result<bool>> UnlinkAsync(CancellationToken ct)
    {
        var master = currentUser.UserId is { } userId ? await masters.FindByUserAsync(userId, ct) : null;
        if (master is null)
        {
            return Result.Failure<bool>(Error.NotFound("telegram.no_master", "Only masters can connect Telegram."));
        }

        master.UnlinkTelegram();
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    /// <summary>Handles a message the bot received. Only "/start CODE" does anything.</summary>
    public async Task HandleMessageAsync(long chatId, string? text, CancellationToken ct)
    {
        const string Start = "/start";
        if (text is null || !text.StartsWith(Start, StringComparison.Ordinal))
        {
            return;
        }

        var code = text[Start.Length..].Trim();
        var target = code.Length == 0 ? null : await links.RedeemAsync(code, clock.GetCurrentInstant(), ct);
        var tenant = target is { } t ? await tenants.FindByIdAsync(t.TenantId, ct) : null;
        if (target is null || tenant is null)
        {
            await telegram.SendAsync(chatId, NotificationTexts.TelegramLinkInvalid, ct);
            return;
        }

        tenantSwitcher.Use(tenant);
        var master = await masters.FindAsync(target.Value.MasterId, ct);
        if (master is null)
        {
            await telegram.SendAsync(chatId, NotificationTexts.TelegramLinkInvalid, ct);
            return;
        }

        master.LinkTelegram(chatId);
        await unitOfWork.SaveChangesAsync(ct);

        var settings = await tenantSettings.GetBookingSettingsAsync(tenant.Id, ct);
        await telegram.SendAsync(chatId, NotificationTexts.TelegramLinked(settings.Language, master.DisplayName), ct);
    }
}
