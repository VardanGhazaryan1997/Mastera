using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mastera.Application.Notifications;
using Mastera.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Mastera.Api.FunctionalTests;

/// <summary>Epic 6: outbox, live notifications, SMS / Telegram, Telegram linking, reminders.</summary>
[Trait("Category", "Integration")]
public sealed class NotificationTests(ApiWithDatabaseFactory factory) : IClassFixture<ApiWithDatabaseFactory>
{
    private Task<List<OutboxMessage>> OutboxForAsync(SalonDriver salon, Guid bookingId) =>
        salon.QueryAsync(db => db.OutboxMessages.AsNoTracking()
            .Where(m => m.Payload.Contains(bookingId.ToString()))
            .ToListAsync(TestContext.Current.CancellationToken));

    [Fact]
    public async Task A_booking_and_its_outbox_message_are_saved_together()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var start = await salon.StartOfAsync("10:00", ct);
        var body = new { masterId = salon.Master, serviceId = salon.Service, start, clientName = "Giorgi", clientPhone = SalonDriver.NewPhone() };

        var booking = await SalonDriver.OkAsync(await salon.Owner.PostAsJsonAsync("/api/bookings/manual", body, ct), HttpStatusCode.Created, ct);
        (await salon.Owner.PostAsJsonAsync("/api/bookings/manual", body, ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var message = (await OutboxForAsync(salon, booking.GetProperty("id").GetGuid())).ShouldHaveSingleItem();
        message.Type.ShouldBe("BookingCreatedEvent");
        message.ProcessedAt.ShouldBeNull();

        // Nothing at all was saved for the rejected second booking.
        var unprocessedForMaster = await salon.QueryAsync(db => db.Bookings.IgnoreQueryFilters()
            .CountAsync(b => b.MasterId == new Domain.Masters.MasterId(salon.Master), ct));
        unprocessedForMaster.ShouldBe(1);
    }

    [Fact]
    public async Task An_online_booking_texts_the_client_once_even_if_the_message_is_delivered_twice()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var phone = SalonDriver.NewPhone();
        var booking = await salon.BookOnlineAsync("10:00", phone, ct);
        var id = booking.GetProperty("id").GetGuid();

        // Two workers at once: SKIP LOCKED gives the message to only one of them.
        var dispatcher = factory.Services.GetRequiredService<OutboxDispatcher>();
        await Task.WhenAll(dispatcher.ProcessPendingAsync(50, ct), dispatcher.ProcessPendingAsync(50, ct));
        await salon.ProcessOutboxAsync(ct);

        var sms = factory.Sms.To(SalonDriver.E164(phone)).ShouldHaveSingleItem();
        sms.Text.ShouldContain("Haircut");
        sms.Text.ShouldContain($"http://{salon.Slug}.book.test/manage/{booking.GetProperty("manageToken").GetString()}");

        // A crash after sending but before marking it processed: the message comes again, the SMS doesn't.
        await salon.QueryAsync(db => db.OutboxMessages.Where(m => m.Payload.Contains(id.ToString()))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedAt, (Instant?)null), ct));
        await salon.ProcessOutboxAsync(ct);

        factory.Sms.To(SalonDriver.E164(phone)).Count.ShouldBe(1);
        (await OutboxForAsync(salon, id)).ShouldAllBe(m => m.ProcessedAt != null);
    }

    [Fact]
    public async Task The_master_and_the_front_desk_get_a_live_notification()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var (master, _) = await salon.SignInMasterAsync(ct);
        var hub = await HubClient.ConnectAsync(master, salon.Slug, ct);

        // Wait for the next message while the booking is made and the outbox processed.
        var next = hub.ReceiveAsync(ct);
        await salon.BookOnlineAsync("11:00", SalonDriver.NewPhone(), ct);
        await salon.ProcessOutboxAsync(ct);

        var message = await next.WaitAsync(TimeSpan.FromSeconds(30), ct);
        message.GetProperty("target").GetString().ShouldBe("notification");
        var notification = message.GetProperty("arguments")[0];
        notification.GetProperty("kind").GetString().ShouldBe(NotificationKinds.Created);
        notification.GetProperty("masterName").GetString().ShouldBe("Luka");
        notification.GetProperty("clientName").GetString().ShouldBe("Nino");
    }

    [Fact]
    public async Task The_hub_refuses_people_who_are_not_signed_in()
    {
        var ct = TestContext.Current.CancellationToken;
        var anonymous = factory.CreatePlatformClient();

        var negotiate = await anonymous.PostAsync($"/hubs/notifications/negotiate?negotiateVersion=1&tenant={ApiWithDatabaseFactory.SeededSlug}", null, ct);

        negotiate.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_master_links_Telegram_with_the_start_code_and_then_hears_about_new_bookings()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var (master, _) = await salon.SignInMasterAsync(ct);
        var chatId = Random.Shared.NextInt64(1_000_000, long.MaxValue);

        var link = await SalonDriver.OkAsync(await master.PostAsync("/api/telegram/link", null, ct), HttpStatusCode.OK, ct);
        var url = link.GetProperty("url").GetString()!;
        url.ShouldStartWith("https://t.me/MasteraTestBot?start=");
        var code = url[(url.IndexOf('=', StringComparison.Ordinal) + 1)..];

        (await WebhookAsync(chatId, $"/start {code}", secret: "wrong", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await WebhookAsync(chatId, $"/start {code}", ApiWithDatabaseFactory.TelegramWebhookSecret, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Telegram.To(chatId).ShouldHaveSingleItem().ShouldContain("Luka");

        // The code works only once.
        await WebhookAsync(chatId, $"/start {code}", ApiWithDatabaseFactory.TelegramWebhookSecret, ct);
        factory.Telegram.To(chatId).Last().ShouldBe(NotificationTexts.TelegramLinkInvalid);

        await salon.BookOnlineAsync("12:00", SalonDriver.NewPhone(), ct);
        await salon.ProcessOutboxAsync(ct);
        factory.Telegram.To(chatId).Last().ShouldContain("Haircut");
    }

    [Fact]
    public async Task An_expired_Telegram_code_does_not_link()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var (master, _) = await salon.SignInMasterAsync(ct);
        var url = (await SalonDriver.OkAsync(await master.PostAsync("/api/telegram/link", null, ct), HttpStatusCode.OK, ct)).GetProperty("url").GetString()!;
        var chatId = Random.Shared.NextInt64(1_000_000, long.MaxValue);

        factory.Clock.Offset = Duration.FromMinutes(31);
        try
        {
            await WebhookAsync(chatId, $"/start {url[(url.IndexOf('=', StringComparison.Ordinal) + 1)..]}", ApiWithDatabaseFactory.TelegramWebhookSecret, ct);
        }
        finally
        {
            factory.Clock.Offset = Duration.Zero;
        }

        factory.Telegram.To(chatId).ShouldHaveSingleItem().ShouldBe(NotificationTexts.TelegramLinkInvalid);
        var linked = (await SalonDriver.OkAsync(await master.PostAsync("/api/telegram/link", null, ct), HttpStatusCode.OK, ct)).GetProperty("linked").GetBoolean();
        linked.ShouldBeFalse();
    }

    [Fact]
    public async Task Reminders_are_planned_moved_with_the_booking_sent_when_due_and_removed_on_cancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var phone = SalonDriver.NewPhone();
        var booking = await salon.BookOnlineAsync("10:00", phone, ct);
        var id = booking.GetProperty("id").GetGuid();
        var token = booking.GetProperty("manageToken").GetString()!;
        await salon.ProcessOutboxAsync(ct);

        var start = SalonDriver.InstantOf(booking.GetProperty("start").GetString()!);
        (await RemindersAsync(salon, id)).Select(r => r.DueAt).ShouldBe([start - Duration.FromHours(24), start - Duration.FromHours(2)], ignoreOrder: true);

        // Moved by an hour: the reminders move too.
        var later = SalonDriver.InstantOf(await salon.StartOfAsync("12:00", ct));
        (await salon.Public.PostAsJsonAsync($"/api/manage/{token}/reschedule", new { start = later.ToDateTimeOffset() }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await salon.ProcessOutboxAsync(ct);
        (await RemindersAsync(salon, id)).Select(r => r.DueAt).ShouldBe([later - Duration.FromHours(24), later - Duration.FromHours(2)], ignoreOrder: true);

        // A day before: the 24 h reminder goes out, once.
        var e164 = SalonDriver.E164(phone);
        var textsBefore = factory.Sms.To(e164).Count;
        var reminders = factory.Services.GetRequiredService<ReminderDispatcher>();
        factory.Clock.Offset = later - Duration.FromHours(23) - SystemClock.Instance.GetCurrentInstant();
        try
        {
            while (await reminders.SendDueAsync(50, ct) > 0)
            {
            }
        }
        finally
        {
            factory.Clock.Offset = Duration.Zero;
        }

        factory.Sms.To(e164).Count.ShouldBe(textsBefore + 1);
        factory.Sms.To(e164).Last().Text.ShouldContain(token);

        // Cancelled: the 2 h reminder is gone.
        (await salon.Public.PostAsync($"/api/manage/{token}/cancel", null, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await salon.ProcessOutboxAsync(ct);
        (await RemindersAsync(salon, id)).ShouldAllBe(r => r.SentAt != null);
    }

    private Task<HttpResponseMessage> WebhookAsync(long chatId, string text, string secret, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/telegram/webhook")
        {
            Content = JsonContent.Create(new { update_id = 1, message = new { message_id = 1, chat = new { id = chatId, type = "private" }, text } }),
        };
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        return factory.CreatePlatformClient().SendAsync(request, ct);
    }

    private static Task<List<ScheduledReminder>> RemindersAsync(SalonDriver salon, Guid bookingId) =>
        salon.QueryAsync(db => db.ScheduledReminders.AsNoTracking().Where(r => r.BookingId == bookingId).ToListAsync(TestContext.Current.CancellationToken));
}

/// <summary>
/// A minimal SignalR client over long polling (JSON protocol), so the tests need no extra package.
/// Messages end with the record separator 0x1E.
/// </summary>
internal sealed class HubClient
{
    private const char Separator = '\u001e';
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly Queue<JsonElement> _pending = new();

    private HubClient(HttpClient http, string url)
    {
        _http = http;
        _url = url;
    }

    public static async Task<HubClient> ConnectAsync(HttpClient http, string slug, CancellationToken ct)
    {
        var negotiate = await http.PostAsync($"/hubs/notifications/negotiate?negotiateVersion=1&tenant={slug}", null, ct);
        negotiate.StatusCode.ShouldBe(HttpStatusCode.OK, await negotiate.Content.ReadAsStringAsync(ct));
        using var body = JsonDocument.Parse(await negotiate.Content.ReadAsStringAsync(ct));
        var token = body.RootElement.GetProperty("connectionToken").GetString()!;
        var client = new HubClient(http, $"/hubs/notifications?tenant={slug}&id={Uri.EscapeDataString(token)}");

        using var handshake = new StringContent("{\"protocol\":\"json\",\"version\":1}" + Separator, Encoding.UTF8, "text/plain");
        (await http.PostAsync(client._url, handshake, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The first poll returns the handshake reply ({}); after it the server has run OnConnectedAsync.
        var reply = await client.PollAsync(ct);
        reply.ShouldStartWith("{}");
        return client;
    }

    /// <summary>The next invocation from the server (pings are skipped).</summary>
    public async Task<JsonElement> ReceiveAsync(CancellationToken ct)
    {
        while (true)
        {
            while (_pending.Count > 0)
            {
                var message = _pending.Dequeue();
                if (message.GetProperty("type").GetInt32() == 1)
                {
                    return message;
                }
            }

            foreach (var frame in (await PollAsync(ct)).Split(Separator, StringSplitOptions.RemoveEmptyEntries))
            {
                using var json = JsonDocument.Parse(frame);
                if (json.RootElement.TryGetProperty("type", out _))
                {
                    _pending.Enqueue(json.RootElement.Clone());
                }
            }
        }
    }

    private async Task<string> PollAsync(CancellationToken ct)
    {
        var response = await _http.GetAsync(_url, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(ct);
    }
}
