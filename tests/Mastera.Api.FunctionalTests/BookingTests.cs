using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mastera.Domain.Memberships;
using NodaTime;

namespace Mastera.Api.FunctionalTests;

/// <summary>Tasks 5.2–5.5: holding a slot, booking online, staff bookings, blocked time.</summary>
[Trait("Category", "Integration")]
public sealed class BookingTests(ApiWithDatabaseFactory factory) : IClassFixture<ApiWithDatabaseFactory>
{
    private static readonly string Day = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(4).AddDays(14)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record Salon(HttpClient Owner, HttpClient Public, string Slug, Guid Service, Guid Master);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response, CancellationToken ct) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();

    private static async Task<JsonElement> CreatedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        return await JsonAsync(response, ct);
    }

    /// <summary>A salon with one master (Luka) working 10:00–14:00 every day and a 60-minute haircut.</summary>
    private async Task<Salon> SalonAsync(CancellationToken ct)
    {
        var (owner, slug) = await factory.SignUpSalonAsync();
        var service = (await CreatedAsync(await owner.PostAsJsonAsync("/api/services", new { name = "Haircut", durationMinutes = 60, price = 35 }, ct), ct)).GetProperty("id").GetGuid();
        var master = (await CreatedAsync(await owner.PostAsJsonAsync("/api/masters", new { displayName = "Luka" }, ct), ct)).GetProperty("id").GetGuid();
        await owner.PutAsJsonAsync($"/api/masters/{master}/services", new { services = new[] { new { serviceId = service } } }, ct);
        var hours = new { start = "10:00", end = "14:00" };
        var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" }.ToDictionary(d => d, _ => (object)hours);
        (await owner.PutAsJsonAsync($"/api/masters/{master}/schedule/pattern", new { pattern = new { kind = "weekly", days } }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        return new Salon(owner, factory.CreateClientFor($"{slug}.{ApiWithDatabaseFactory.RootDomain}"), slug, service, master);
    }

    private static async Task<List<JsonElement>> SlotsAsync(HttpClient client, Salon salon, CancellationToken ct) =>
        (await JsonAsync(await client.GetAsync($"/api/availability?serviceId={salon.Service}&date={Day}", ct), ct))
            .GetProperty("slots").EnumerateArray().ToList();

    private static async Task<string> StartOfAsync(Salon salon, string time, CancellationToken ct) =>
        (await SlotsAsync(salon.Public, salon, ct)).Single(s => s.GetProperty("time").GetString() == time).GetProperty("start").GetString()!;

    private static Task<HttpResponseMessage> HoldAsync(Salon salon, string start, CancellationToken ct) =>
        salon.Public.PostAsJsonAsync("/api/holds", new { serviceId = salon.Service, start }, ct);

    private static Task<HttpResponseMessage> BookAsync(Salon salon, Guid holdId, string phone, CancellationToken ct, string name = "Nino") =>
        salon.Public.PostAsJsonAsync("/api/bookings", new { holdId, name, phone }, ct);

    private static List<string?> Times(IEnumerable<JsonElement> slots) => slots.Select(s => s.GetProperty("time").GetString()).ToList();

    [Fact]
    public async Task A_client_holds_a_slot_then_books_it_and_the_time_is_no_longer_offered()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonAsync(ct);
        var start = await StartOfAsync(salon, "10:00", ct);

        var hold = await CreatedAsync(await HoldAsync(salon, start, ct), ct);
        hold.GetProperty("masterName").GetString().ShouldBe("Luka");
        hold.GetProperty("price").GetDecimal().ShouldBe(35m);
        Times(await SlotsAsync(salon.Public, salon, ct)).ShouldNotContain("10:00");
        Times(await SlotsAsync(salon.Public, salon, ct)).ShouldNotContain("10:45");

        var booking = await CreatedAsync(await BookAsync(salon, hold.GetProperty("id").GetGuid(), "+995 555 12 34 56", ct), ct);
        booking.GetProperty("status").GetString().ShouldBe("Confirmed");
        booking.GetProperty("serviceName").GetString().ShouldBe("Haircut");

        Times(await SlotsAsync(salon.Public, salon, ct)).ShouldBe(["11:00", "11:15", "11:30", "11:45", "12:00", "12:15", "12:30", "12:45", "13:00"]);

        var calendar = await JsonAsync(await salon.Owner.GetAsync($"/api/calendar?from={Day}T00:00:00Z&to={Day}T23:59:59Z", ct), ct);
        var entry = calendar.GetProperty("bookings").EnumerateArray().ShouldHaveSingleItem();
        entry.GetProperty("clientName").GetString().ShouldBe("Nino");
        entry.GetProperty("clientPhone").GetString().ShouldBe("+995555123456");
        entry.GetProperty("source").GetString().ShouldBe("Online");
    }

    [Fact]
    public async Task A_slot_held_by_someone_else_returns_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonAsync(ct);
        var start = await StartOfAsync(salon, "11:00", ct);
        await CreatedAsync(await HoldAsync(salon, start, ct), ct);

        var second = await HoldAsync(salon, start, ct);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.ProblemCodeAsync(ct)).ShouldBe("booking.slot_taken");
    }

    [Fact]
    public async Task An_expired_hold_cannot_be_booked_and_its_time_is_free_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonAsync(ct);
        var start = await StartOfAsync(salon, "12:00", ct);
        var hold = await CreatedAsync(await HoldAsync(salon, start, ct), ct);

        factory.Clock.Offset = Duration.FromMinutes(11);
        try
        {
            var late = await BookAsync(salon, hold.GetProperty("id").GetGuid(), "555123456", ct);
            late.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await late.ProblemCodeAsync(ct)).ShouldBe("booking.hold.expired");

            Times(await SlotsAsync(salon.Public, salon, ct)).ShouldContain("12:00");
            await CreatedAsync(await HoldAsync(salon, start, ct), ct);
        }
        finally
        {
            factory.Clock.Offset = Duration.Zero;
        }
    }

    [Fact]
    public async Task An_invalid_phone_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonAsync(ct);
        var hold = await CreatedAsync(await HoldAsync(salon, await StartOfAsync(salon, "10:00", ct), ct), ct);

        var response = await BookAsync(salon, hold.GetProperty("id").GetGuid(), "12345", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync(ct)).ShouldBe("client.phone.invalid");
    }

    [Fact]
    public async Task A_returning_client_is_recognised_by_phone_however_it_is_typed()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonAsync(ct);
        var first = await CreatedAsync(await HoldAsync(salon, await StartOfAsync(salon, "10:00", ct), ct), ct);
        await CreatedAsync(await BookAsync(salon, first.GetProperty("id").GetGuid(), "555 12 34 56", ct), ct);
        var second = await CreatedAsync(await HoldAsync(salon, await StartOfAsync(salon, "12:00", ct), ct), ct);
        await CreatedAsync(await BookAsync(salon, second.GetProperty("id").GetGuid(), "+995-555-123-456", ct, name: "Nino K."), ct);

        var calendar = await JsonAsync(await salon.Owner.GetAsync($"/api/calendar?from={Day}T00:00:00Z&to={Day}T23:59:59Z", ct), ct);

        calendar.GetProperty("bookings").EnumerateArray().Select(b => b.GetProperty("clientId").GetGuid()).Distinct().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Staff_enter_a_phone_booking_and_a_walk_in_but_never_over_another_booking()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonAsync(ct);
        var tenOClock = await StartOfAsync(salon, "10:00", ct);
        var halfPast = await StartOfAsync(salon, "10:30", ct);
        var noon = await StartOfAsync(salon, "12:00", ct);

        var phoneBooking = await CreatedAsync(await salon.Owner.PostAsJsonAsync("/api/bookings/manual",
            new { masterId = salon.Master, serviceId = salon.Service, start = tenOClock, clientName = "Giorgi", clientPhone = "599 11 22 33" }, ct), ct);
        phoneBooking.GetProperty("status").GetString().ShouldBe("Confirmed");

        var overlapping = await salon.Owner.PostAsJsonAsync("/api/bookings/manual",
            new { masterId = salon.Master, serviceId = salon.Service, start = halfPast, clientName = "Ana", clientPhone = "599 44 55 66" }, ct);
        overlapping.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await overlapping.ProblemCodeAsync(ct)).ShouldBe("booking.slot_taken");

        await CreatedAsync(await salon.Owner.PostAsJsonAsync("/api/bookings/manual",
            new { masterId = salon.Master, serviceId = salon.Service, start = noon, clientName = "Walk-in guest", walkIn = true }, ct), ct);

        var calendar = await JsonAsync(await salon.Owner.GetAsync($"/api/calendar?from={Day}T00:00:00Z&to={Day}T23:59:59Z", ct), ct);
        var bookings = calendar.GetProperty("bookings").EnumerateArray().ToList();
        bookings.Select(b => b.GetProperty("source").GetString()).ShouldBe(["Manual", "WalkIn"]);
        bookings[1].GetProperty("clientName").GetString().ShouldBe("Walk-in guest");
        bookings[1].GetProperty("clientPhone").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_phone_booking_needs_a_phone_number()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonAsync(ct);

        var response = await salon.Owner.PostAsJsonAsync("/api/bookings/manual",
            new { masterId = salon.Master, serviceId = salon.Service, start = await StartOfAsync(salon, "10:00", ct), clientName = "Giorgi" }, ct);

        (await response.ProblemCodeAsync(ct)).ShouldBe("client.phone.invalid");
    }

    [Fact]
    public async Task Masters_cannot_enter_bookings_for_the_salon()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = await factory.CreateUserAsync(MembershipRole.Master);
        var client = factory.CreatePlatformClient();
        await client.LoginAsync(email, ApiWithDatabaseFactory.Password, ct);
        client.DefaultRequestHeaders.Add("X-Mastera-Tenant", ApiWithDatabaseFactory.SeededSlug);

        var response = await client.PostAsJsonAsync("/api/bookings/manual",
            new { masterId = Guid.NewGuid(), serviceId = Guid.NewGuid(), start = DateTimeOffset.UtcNow.AddDays(3), clientName = "X", clientPhone = "555123456" }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Blocked_time_removes_slots_until_it_is_removed()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonAsync(ct);
        var ten = DateTimeOffset.Parse(await StartOfAsync(salon, "10:00", ct), CultureInfo.InvariantCulture);

        var blocked = await CreatedAsync(await salon.Owner.PostAsJsonAsync("/api/blocked-times",
            new { masterId = salon.Master, start = ten, end = ten.AddHours(1), reason = "Training" }, ct), ct);

        Times(await SlotsAsync(salon.Public, salon, ct))[0].ShouldBe("11:00");

        var calendar = await JsonAsync(await salon.Owner.GetAsync($"/api/calendar?from={Day}T00:00:00Z&to={Day}T23:59:59Z", ct), ct);
        calendar.GetProperty("blocked").EnumerateArray().ShouldHaveSingleItem().GetProperty("reason").GetString().ShouldBe("Training");

        (await salon.Owner.DeleteAsync($"/api/blocked-times/{blocked.GetProperty("id").GetGuid()}", ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Times(await SlotsAsync(salon.Public, salon, ct))[0].ShouldBe("10:00");
    }

    [Fact]
    public async Task Blocked_time_cannot_cover_a_booking()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonAsync(ct);
        var ten = await StartOfAsync(salon, "10:00", ct);
        await CreatedAsync(await salon.Owner.PostAsJsonAsync("/api/bookings/manual",
            new { masterId = salon.Master, serviceId = salon.Service, start = ten, clientName = "Giorgi", clientPhone = "599112233" }, ct), ct);
        var start = DateTimeOffset.Parse(ten, CultureInfo.InvariantCulture);

        var response = await salon.Owner.PostAsJsonAsync("/api/blocked-times", new { masterId = salon.Master, start, end = start.AddMinutes(30) }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync(ct)).ShouldBe("blocked.overlaps");
    }
}

/// <summary>Task 5.4: the public endpoints are rate limited per address.</summary>
[Trait("Category", "Integration")]
public sealed class RateLimitTests(RateLimitTests.Factory factory) : IClassFixture<RateLimitTests.Factory>
{
    public sealed class Factory : ApiWithDatabaseFactory
    {
        protected override int PublicBookingLimit => 3;
    }

    [Fact]
    public async Task Too_many_public_booking_calls_return_429()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClientFor($"{ApiWithDatabaseFactory.SeededSlug}.{ApiWithDatabaseFactory.RootDomain}");
        var body = new { serviceId = Guid.NewGuid(), start = DateTimeOffset.UtcNow.AddDays(3) };

        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/api/holds", body, ct)).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }

        var limited = await client.PostAsJsonAsync("/api/holds", body, ct);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await limited.ProblemCodeAsync(ct)).ShouldBe("rate_limited");
    }
}
