using System.Net.Http.Json;
using Mastera.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mastera.Infrastructure.Notifications;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>The SMS gateway's endpoint. When empty, messages are only written to the log (development).</summary>
    public Uri? Url { get; set; }

    public string? ApiKey { get; set; }

    public string Sender { get; set; } = "Mastera";
}

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>From @BotFather. When empty, Telegram messages are only written to the log.</summary>
    public string? BotToken { get; set; }

    /// <summary>The bot's username without "@", used for the t.me link.</summary>
    public string? BotName { get; set; }

    /// <summary>Must match the secret_token given to setWebhook; Telegram sends it in every webhook call.</summary>
    public string? WebhookSecret { get; set; }

    public Uri ApiBaseUrl { get; set; } = new("https://api.telegram.org");
}

/// <summary>
/// Task 6.4: a generic HTTP SMS gateway: POST {Url} with JSON { to, text, sender } and a Bearer API key.
/// Most Georgian gateways accept this shape or need a small adapter. Without a URL, messages are only logged.
/// </summary>
public sealed class HttpSmsSender(HttpClient http, IOptions<SmsOptions> options, ILogger<HttpSmsSender> logger) : ISmsSender
{
    public async Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        if (settings.Url is null)
        {
            LogOnly(logger, message.To.Masked(), message.Text, null);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Url)
        {
            Content = JsonContent.Create(new { to = message.To.Value, text = message.Text, sender = settings.Sender }),
        };
        if (!string.IsNullOrEmpty(settings.ApiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey);
        }

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static readonly Action<ILogger, string, string, Exception?> LogOnly =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(6401, "SmsLogOnly"), "SMS (not sent, no gateway configured) to {To}: {Text}");
}

/// <summary>Task 6.4: Telegram Bot API sendMessage. Without a bot token, messages are only logged.</summary>
public sealed class TelegramBotSender(HttpClient http, IOptions<TelegramOptions> options, ILogger<TelegramBotSender> logger) : ITelegramSender
{
    public async Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrEmpty(settings.BotToken))
        {
            LogOnly(logger, chatId, text, null);
            return;
        }

        var url = new Uri(settings.ApiBaseUrl, $"/bot{settings.BotToken}/sendMessage");
        using var response = await http.PostAsJsonAsync(url, new { chat_id = chatId, text }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static readonly Action<ILogger, long, string, Exception?> LogOnly =
        LoggerMessage.Define<long, string>(LogLevel.Information, new EventId(6402, "TelegramLogOnly"), "Telegram (not sent, no bot token) to {ChatId}: {Text}");
}
