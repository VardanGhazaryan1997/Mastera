using System.Net;
using System.Text.Json;
using Mastera.Application.Notifications;
using Mastera.Domain.Bookings;
using Mastera.Domain.Clients;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Email;
using Mastera.Infrastructure.Notifications;
using Mastera.Infrastructure.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Mastera.Infrastructure.IntegrationTests;

/// <summary>Task 6.4 contract tests (no network: a fake HTTP handler records the requests), plus links and event JSON.</summary>
public sealed class NotificationAdapterTests
{
    private sealed class RecordingHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent("{\"ok\":true}") };
        }
    }

    [Fact]
    public async Task Telegram_messages_are_sent_with_sendMessage()
    {
        var handler = new RecordingHandler();
        var sender = new TelegramBotSender(new HttpClient(handler), Options.Create(new TelegramOptions { BotToken = "123:ABC" }), NullLogger<TelegramBotSender>.Instance);

        await sender.SendAsync(42, "Hello", TestContext.Current.CancellationToken);

        var (request, body) = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe("https://api.telegram.org/bot123:ABC/sendMessage");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("chat_id").GetInt64().ShouldBe(42);
        json.RootElement.GetProperty("text").GetString().ShouldBe("Hello");
    }

    [Fact]
    public async Task Without_a_bot_token_nothing_is_sent()
    {
        var handler = new RecordingHandler();
        var sender = new TelegramBotSender(new HttpClient(handler), Options.Create(new TelegramOptions()), NullLogger<TelegramBotSender>.Instance);

        await sender.SendAsync(42, "Hello", TestContext.Current.CancellationToken);

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sms_is_posted_to_the_gateway_with_the_api_key()
    {
        var handler = new RecordingHandler();
        var options = Options.Create(new SmsOptions { Url = new Uri("https://sms.example/send"), ApiKey = "key-1", Sender = "Salon" });
        var sender = new HttpSmsSender(new HttpClient(handler), options, NullLogger<HttpSmsSender>.Instance);

        await sender.SendAsync(new SmsMessage(PhoneNumber.Create("555123456"), "Hi"), TestContext.Current.CancellationToken);

        var (request, body) = handler.Requests.ShouldHaveSingleItem();
        request.RequestUri.ShouldBe(new Uri("https://sms.example/send"));
        request.Headers.Authorization!.ToString().ShouldBe("Bearer key-1");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("to").GetString().ShouldBe("+995555123456");
        json.RootElement.GetProperty("text").GetString().ShouldBe("Hi");
        json.RootElement.GetProperty("sender").GetString().ShouldBe("Salon");
    }

    [Fact]
    public async Task A_gateway_error_fails_the_send_so_the_outbox_retries_it()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadGateway);
        var sender = new HttpSmsSender(new HttpClient(handler), Options.Create(new SmsOptions { Url = new Uri("https://sms.example/send") }), NullLogger<HttpSmsSender>.Instance);

        await Should.ThrowAsync<HttpRequestException>(() =>
            sender.SendAsync(new SmsMessage(PhoneNumber.Create("555123456"), "Hi"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Manage_tokens_round_trip_and_reject_tampering_and_other_secrets()
    {
        var links = new BookingLinks(Options.Create(new AppLinkOptions { LinkSecret = "secret-a", BookingSiteUrl = "https://{slug}.mastera.ge" }));
        var id = BookingId.New();

        var token = links.CreateManageToken(id);

        links.ReadManageToken(token).ShouldBe(id);
        token.ShouldMatch("^[A-Za-z0-9_-]+$");
        links.ReadManageToken(token[..20] + (token[20] == 'A' ? 'B' : 'A') + token[21..]).ShouldBeNull();
        links.ReadManageToken("not a token").ShouldBeNull();
        links.ReadManageToken(null).ShouldBeNull();
        new BookingLinks(Options.Create(new AppLinkOptions { LinkSecret = "secret-b" })).ReadManageToken(token).ShouldBeNull();
        links.ManageUrl(TenantSlug.Create("barber-house"), token).ShouldBe($"https://barber-house.mastera.ge/manage/{token}");
    }

    [Fact]
    public void Domain_events_survive_the_trip_through_the_outbox()
    {
        var original = new BookingRescheduledEvent(BookingId.New(), Instant.FromUtc(2026, 11, 2, 8, 30));

        var (type, payload) = DomainEventSerializer.Serialize(original);

        type.ShouldBe(nameof(BookingRescheduledEvent));
        DomainEventSerializer.Deserialize(type, payload).ShouldBe(original);
    }
}
