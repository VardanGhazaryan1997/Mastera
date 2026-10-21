using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mastera.Infrastructure.Outbox;
using Mastera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Mastera.Api.FunctionalTests;

/// <summary>Shared setup for the Epic 5/6 tests: a salon, a master working 10:00–14:00 daily, online bookings.</summary>
internal sealed partial class SalonDriver(ApiWithDatabaseFactory factory)
{
    public static readonly string Day = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(4).AddDays(14)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public HttpClient Owner { get; private set; } = null!;

    public HttpClient Public { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public Guid Service { get; private set; }

    public Guid Master { get; private set; }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response, CancellationToken ct) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();

    public static async Task<JsonElement> OkAsync(HttpResponseMessage response, HttpStatusCode status, CancellationToken ct)
    {
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync(ct));
        return await JsonAsync(response, ct);
    }

    /// <summary>A Georgian mobile number nobody else in the test run uses.</summary>
    public static string NewPhone() => "5" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    public static string E164(string phone) => "+995" + phone;

    public async Task<SalonDriver> SetUpAsync(CancellationToken ct)
    {
        (Owner, Slug) = await factory.SignUpSalonAsync();
        Service = (await OkAsync(await Owner.PostAsJsonAsync("/api/services", new { name = "Haircut", durationMinutes = 60, price = 35 }, ct), HttpStatusCode.Created, ct)).GetProperty("id").GetGuid();
        Master = (await OkAsync(await Owner.PostAsJsonAsync("/api/masters", new { displayName = "Luka" }, ct), HttpStatusCode.Created, ct)).GetProperty("id").GetGuid();
        await Owner.PutAsJsonAsync($"/api/masters/{Master}/services", new { services = new[] { new { serviceId = Service } } }, ct);
        var hours = new { start = "10:00", end = "14:00" };
        var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" }.ToDictionary(d => d, _ => (object)hours);
        (await Owner.PutAsJsonAsync($"/api/masters/{Master}/schedule/pattern", new { pattern = new { kind = "weekly", days } }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        Public = factory.CreateClientFor($"{Slug}.{ApiWithDatabaseFactory.RootDomain}");
        return this;
    }

    public async Task<List<string?>> TimesAsync(CancellationToken ct) =>
        (await JsonAsync(await Public.GetAsync($"/api/availability?serviceId={Service}&date={Day}", ct), ct))
            .GetProperty("slots").EnumerateArray().Select(s => s.GetProperty("time").GetString()).ToList();

    public async Task<string> StartOfAsync(string time, CancellationToken ct) =>
        (await JsonAsync(await Public.GetAsync($"/api/availability?serviceId={Service}&date={Day}", ct), ct))
            .GetProperty("slots").EnumerateArray().Single(s => s.GetProperty("time").GetString() == time).GetProperty("start").GetString()!;

    /// <summary>Books online like a client: hold, then book. Returns the booking (with its manageToken).</summary>
    public async Task<JsonElement> BookOnlineAsync(string time, string phone, CancellationToken ct, string? email = null)
    {
        var hold = await OkAsync(await Public.PostAsJsonAsync("/api/holds", new { serviceId = Service, start = await StartOfAsync(time, ct) }, ct), HttpStatusCode.Created, ct);
        return await OkAsync(await Public.PostAsJsonAsync("/api/bookings", new { holdId = hold.GetProperty("id").GetGuid(), name = "Nino", phone, email }, ct), HttpStatusCode.Created, ct);
    }

    /// <summary>Invites a master user for <see cref="Master"/> and returns a client signed in as them (cookie kept by hand).</summary>
    public async Task<(HttpClient Client, string Cookie)> SignInMasterAsync(CancellationToken ct)
    {
        var email = $"master-{Guid.NewGuid():N}@example.ge";
        (await Owner.PostAsJsonAsync("/api/invitations", new { email, role = "Master", masterId = Master }, ct)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var token = Uri.UnescapeDataString(InviteLink().Match(factory.Emails.LastTo(email).TextBody).Groups["token"].Value);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"http://{ApiWithDatabaseFactory.RootDomain}"),
            HandleCookies = false,
        });
        var accept = await client.PostAsJsonAsync("/api/invitations/accept", new { token, displayName = "Luka", password = ApiWithDatabaseFactory.Password }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync(ct));
        var cookie = string.Join("; ", accept.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        client.DefaultRequestHeaders.Add("X-Mastera-Tenant", Slug);
        return (client, cookie);
    }

    /// <summary>Runs the outbox processor until nothing is left (the background worker is off in tests).</summary>
    public async Task ProcessOutboxAsync(CancellationToken ct)
    {
        var dispatcher = factory.Services.GetRequiredService<OutboxDispatcher>();
        while (await dispatcher.ProcessPendingAsync(50, ct) > 0)
        {
        }
    }

    public async Task<T> QueryAsync<T>(Func<MasteraDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<MasteraDbContext>());
    }

    public static Instant InstantOf(string iso) => Instant.FromDateTimeOffset(DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture));

    [GeneratedRegex(@"http://dashboard\.test/invite\?token=(?<token>[A-Za-z0-9_%\-]+)")]
    private static partial Regex InviteLink();
}

/// <summary>Tasks 5.6–5.8: the client's manage link, completing / no-show, hiding phone numbers from masters.</summary>
[Trait("Category", "Integration")]
public sealed class BookingLifecycleTests(ApiWithDatabaseFactory factory) : IClassFixture<ApiWithDatabaseFactory>
{
    [Fact]
    public async Task A_client_sees_and_cancels_their_booking_with_the_link_and_the_time_is_free_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var booking = await salon.BookOnlineAsync("10:00", SalonDriver.NewPhone(), ct);
        var token = booking.GetProperty("manageToken").GetString()!;

        var view = await SalonDriver.OkAsync(await salon.Public.GetAsync($"/api/manage/{token}", ct), HttpStatusCode.OK, ct);
        view.GetProperty("serviceName").GetString().ShouldBe("Haircut");
        view.GetProperty("canChange").GetBoolean().ShouldBeTrue();

        var cancelled = await SalonDriver.OkAsync(await salon.Public.PostAsync($"/api/manage/{token}/cancel", null, ct), HttpStatusCode.OK, ct);
        cancelled.GetProperty("status").GetString().ShouldBe("CancelledByClient");
        cancelled.GetProperty("canChange").GetBoolean().ShouldBeFalse();
        (await salon.TimesAsync(ct)).ShouldContain("10:00");
    }

    [Fact]
    public async Task A_changed_link_or_a_link_from_another_salon_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var other = await new SalonDriver(factory).SetUpAsync(ct);
        var token = (await salon.BookOnlineAsync("10:00", SalonDriver.NewPhone(), ct)).GetProperty("manageToken").GetString()!;
        var tampered = (token[0] == 'A' ? "B" : "A") + token[1..];

        var bad = await salon.Public.GetAsync($"/api/manage/{tampered}", ct);
        bad.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bad.ProblemCodeAsync(ct)).ShouldBe("booking.link.invalid");
        (await other.Public.GetAsync($"/api/manage/{token}", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Inside_the_cancellation_window_the_link_can_no_longer_cancel()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var booking = await salon.BookOnlineAsync("12:00", SalonDriver.NewPhone(), ct);
        var token = booking.GetProperty("manageToken").GetString()!;
        var start = SalonDriver.InstantOf(booking.GetProperty("start").GetString()!);

        factory.Clock.Offset = start - Duration.FromMinutes(90) - SystemClock.Instance.GetCurrentInstant();
        try
        {
            var late = await salon.Public.PostAsync($"/api/manage/{token}/cancel", null, ct);
            late.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await late.ProblemCodeAsync(ct)).ShouldBe("booking.change.too_late");
        }
        finally
        {
            factory.Clock.Offset = Duration.Zero;
        }
    }

    [Fact]
    public async Task A_client_moves_their_booking_to_another_free_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var token = (await salon.BookOnlineAsync("10:00", SalonDriver.NewPhone(), ct)).GetProperty("manageToken").GetString()!;
        await salon.BookOnlineAsync("12:00", SalonDriver.NewPhone(), ct);

        // 10:30 overlaps only the booking being moved, so moving there is allowed.
        var start = await StartOfAsync(salon, "11:00", ct);
        var nearby = await SalonDriver.OkAsync(await salon.Public.PostAsJsonAsync($"/api/manage/{token}/reschedule",
            new { start = DateTimeOffset.Parse(start, CultureInfo.InvariantCulture).AddMinutes(-30) }, ct), HttpStatusCode.OK, ct);
        nearby.GetProperty("status").GetString().ShouldBe("Confirmed");

        var moved = await SalonDriver.OkAsync(await salon.Public.PostAsJsonAsync($"/api/manage/{token}/reschedule", new { start }, ct), HttpStatusCode.OK, ct);
        var times = await salon.TimesAsync(ct);
        times.ShouldContain("10:00");
        times.ShouldNotContain("11:00");

        // 11:30 would run into the 12:00 booking.
        var taken = await salon.Public.PostAsJsonAsync($"/api/manage/{token}/reschedule", new { start = DateTimeOffset.Parse(moved.GetProperty("start").GetString()!, CultureInfo.InvariantCulture).AddMinutes(30) }, ct);
        taken.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await taken.ProblemCodeAsync(ct)).ShouldBe("booking.slot_taken");
    }

    [Fact]
    public async Task Staff_complete_with_the_final_price_and_record_no_shows_on_the_client()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var phone = SalonDriver.NewPhone();
        var first = (await salon.BookOnlineAsync("10:00", phone, ct)).GetProperty("id").GetGuid();
        var second = (await salon.BookOnlineAsync("12:00", phone, ct)).GetProperty("id").GetGuid();

        var early = await salon.Owner.PostAsJsonAsync($"/api/bookings/{first}/complete", new { finalPrice = 40 }, ct);
        (await early.ProblemCodeAsync(ct)).ShouldBe("booking.not_started");

        factory.Clock.Offset = Duration.FromDays(15);
        try
        {
            var done = await SalonDriver.OkAsync(await salon.Owner.PostAsJsonAsync($"/api/bookings/{first}/complete", new { finalPrice = 40 }, ct), HttpStatusCode.OK, ct);
            done.GetProperty("status").GetString().ShouldBe("Completed");
            done.GetProperty("finalPrice").GetDecimal().ShouldBe(40m);

            var noShow = await SalonDriver.OkAsync(await salon.Owner.PostAsync($"/api/bookings/{second}/no-show", null, ct), HttpStatusCode.OK, ct);
            noShow.GetProperty("status").GetString().ShouldBe("NoShow");

            var again = await salon.Owner.PostAsync($"/api/bookings/{second}/no-show", null, ct);
            (await again.ProblemCodeAsync(ct)).ShouldBe("booking.transition.invalid");
        }
        finally
        {
            factory.Clock.Offset = Duration.Zero;
        }

        var e164 = SalonDriver.E164(phone);
        var count = await salon.QueryAsync(db => db.Clients.IgnoreQueryFilters()
            .Where(c => c.Phone == Domain.Clients.PhoneNumber.Create(e164)).Select(c => c.NoShowCount).SingleAsync(ct));
        count.ShouldBe(1);
    }

    [Fact]
    public async Task Staff_cancel_a_booking_and_the_time_is_free_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var id = (await salon.BookOnlineAsync("11:00", SalonDriver.NewPhone(), ct)).GetProperty("id").GetGuid();

        var cancelled = await SalonDriver.OkAsync(await salon.Owner.PostAsync($"/api/bookings/{id}/cancel", null, ct), HttpStatusCode.OK, ct);

        cancelled.GetProperty("status").GetString().ShouldBe("CancelledBySalon");
        (await salon.TimesAsync(ct)).ShouldContain("11:00");
    }

    [Fact]
    public async Task Masters_see_masked_phone_numbers_when_the_salon_hides_them()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await new SalonDriver(factory).SetUpAsync(ct);
        var phone = SalonDriver.NewPhone();
        await salon.BookOnlineAsync("10:00", phone, ct);
        var (master, _) = await salon.SignInMasterAsync(ct);
        var calendarUrl = $"/api/calendar?from={SalonDriver.Day}T00:00:00Z&to={SalonDriver.Day}T23:59:59Z";

        async Task<string?> PhoneSeenBy(HttpClient client) =>
            (await SalonDriver.OkAsync(await client.GetAsync(calendarUrl, ct), HttpStatusCode.OK, ct))
                .GetProperty("bookings").EnumerateArray().Single().GetProperty("clientPhone").GetString();

        (await PhoneSeenBy(master)).ShouldBe(SalonDriver.E164(phone));

        (await master.PutAsJsonAsync("/api/settings/booking", new { hideClientPhoneFromMasters = true, cancellationWindowHours = 2 }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await salon.Owner.PutAsJsonAsync("/api/settings/booking", new { hideClientPhoneFromMasters = true, cancellationWindowHours = 3 }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var masked = await PhoneSeenBy(master);
        masked.ShouldStartWith("+995");
        masked.ShouldEndWith(phone[^3..]);
        masked.ShouldNotContain(phone[..5]);
        (await PhoneSeenBy(salon.Owner)).ShouldBe(SalonDriver.E164(phone));

        var settings = await SalonDriver.OkAsync(await salon.Owner.GetAsync("/api/settings/booking", ct), HttpStatusCode.OK, ct);
        settings.GetProperty("cancellationWindowHours").GetInt32().ShouldBe(3);
    }

    private static Task<string> StartOfAsync(SalonDriver salon, string time, CancellationToken ct) => salon.StartOfAsync(time, ct);
}
