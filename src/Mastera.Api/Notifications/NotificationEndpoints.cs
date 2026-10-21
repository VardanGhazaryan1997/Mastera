using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Mastera.Api.Auth;
using Mastera.Api.Common;
using Mastera.Application.Notifications;
using Mastera.Infrastructure.Notifications;
using Microsoft.Extensions.Options;

namespace Mastera.Api.Notifications;

public sealed record TelegramChat([property: JsonPropertyName("id")] long Id);

public sealed record TelegramMessage([property: JsonPropertyName("chat")] TelegramChat Chat, [property: JsonPropertyName("text")] string? Text);

/// <summary>The part of a Telegram webhook update we use.</summary>
public sealed record TelegramUpdate([property: JsonPropertyName("message")] TelegramMessage? Message);

public sealed record TelegramLinkResponse(string Url, DateTimeOffset ExpiresAt, bool Linked);

/// <summary>Tasks 6.3 and 6.5: the live-notification hub and Telegram linking for masters.</summary>
public static class NotificationEndpoints
{
    public const string HubPath = "/hubs/notifications";
    public const string TelegramSecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHub<NotificationsHub>(HubPath);

        // A master asks for a link; opening it in Telegram sends "/start CODE" to the bot.
        app.MapPost("/api/telegram/link", async (TelegramLinkHandler handler, IOptions<TelegramOptions> options, CancellationToken ct) =>
        {
            var result = await handler.CreateCodeAsync(ct);
            if (!result.IsSuccess)
            {
                return result.Error!.ToProblem();
            }

            var bot = options.Value.BotName ?? "MasteraBot";
            var link = result.Value!;
            return Results.Ok(new TelegramLinkResponse($"https://t.me/{bot}?start={link.Code}", link.ExpiresAt, link.Linked));
        }).RequireAuthorization(Policies.SalonMember).WithTags("Notifications");

        app.MapDelete("/api/telegram/link", async (TelegramLinkHandler handler, CancellationToken ct) =>
        {
            var result = await handler.UnlinkAsync(ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).RequireAuthorization(Policies.SalonMember).WithTags("Notifications");

        // Telegram calls this for every message to the bot (set up with setWebhook and a secret_token).
        app.MapPost("/api/telegram/webhook", async (HttpRequest request, TelegramUpdate update, TelegramLinkHandler handler, IOptions<TelegramOptions> options, CancellationToken ct) =>
        {
            var secret = options.Value.WebhookSecret;
            if (string.IsNullOrEmpty(secret) || !SecretMatches(request.Headers[TelegramSecretHeader].ToString(), secret))
            {
                return Results.NotFound();
            }

            if (update.Message is { } message)
            {
                await handler.HandleMessageAsync(message.Chat.Id, message.Text, ct);
            }

            return Results.Ok();
        }).AllowAnonymous().ExcludeFromDescription();

        return app;
    }

    private static bool SecretMatches(string given, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected));
}
