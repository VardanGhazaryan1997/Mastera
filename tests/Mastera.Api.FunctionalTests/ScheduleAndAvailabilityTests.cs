using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mastera.Domain.Memberships;

namespace Mastera.Api.FunctionalTests;

/// <summary>Tasks 3.4 and 4.6: saving schedules and asking for free times.</summary>
[Trait("Category", "Integration")]
public sealed class ScheduleAndAvailabilityTests(ApiWithDatabaseFactory factory) : IClassFixture<ApiWithDatabaseFactory>
{
    /// <summary>A date far enough ahead to pass the 1-hour notice, inside the 60-day horizon: 14 days from now, in Tbilisi.</summary>
    private static readonly DateOnly Day = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(4).AddDays(14));

    private static readonly object TenToTwo = new { start = "10:00", end = "14:00", breaks = new[] { new { start = "12:00", end = "13:00" } } };

    private static object EveryDay(object hours) => new
    {
        pattern = new
        {
            kind = "weekly",
            days = new Dictionary<string, object>
            {
                ["Monday"] = hours, ["Tuesday"] = hours, ["Wednesday"] = hours, ["Thursday"] = hours,
                ["Friday"] = hours, ["Saturday"] = hours, ["Sunday"] = hours,
            },
        },
    };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response, CancellationToken ct) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();

    private static async Task<Guid> CreateAsync(HttpClient client, string url, object body, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(url, body, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        return (await JsonAsync(response, ct)).GetProperty("id").GetGuid();
    }

    private sealed record Salon(HttpClient Owner, string Slug, Guid Service, Guid Master);

    private async Task<Salon> SalonWithOneMasterAsync(CancellationToken ct, int durationMinutes = 60)
    {
        var (owner, slug) = await factory.SignUpSalonAsync();
        var service = await CreateAsync(owner, "/api/services", new { name = "Haircut", durationMinutes, price = 35 }, ct);
        var master = await CreateAsync(owner, "/api/masters", new { displayName = "Luka" }, ct);
        (await owner.PutAsJsonAsync($"/api/masters/{master}/services", new { services = new[] { new { serviceId = service } } }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        return new Salon(owner, slug, service, master);
    }

    private static string DayString => Day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Owner_saves_a_weekly_pattern_and_reads_it_back()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonWithOneMasterAsync(ct);

        var saved = await salon.Owner.PutAsJsonAsync($"/api/masters/{salon.Master}/schedule/pattern", EveryDay(TenToTwo), ct);

        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var schedule = await JsonAsync(await salon.Owner.GetAsync($"/api/masters/{salon.Master}/schedule", ct), ct);
        var monday = schedule.GetProperty("pattern").GetProperty("days").GetProperty("Monday");
        monday.GetProperty("start").GetString().ShouldBe("10:00");
        monday.GetProperty("breaks")[0].GetProperty("end").GetString().ShouldBe("13:00");
    }

    [Fact]
    public async Task Invalid_patterns_are_rejected_with_a_code()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonWithOneMasterAsync(ct);

        var badTime = await salon.Owner.PutAsJsonAsync($"/api/masters/{salon.Master}/schedule/pattern", EveryDay(new { start = "25:00", end = "14:00" }), ct);
        (await badTime.ProblemCodeAsync(ct)).ShouldBe("schedule.time.invalid");

        var overlap = await salon.Owner.PutAsJsonAsync(
            $"/api/masters/{salon.Master}/schedule/pattern",
            EveryDay(new { start = "10:00", end = "18:00", breaks = new[] { new { start = "12:00", end = "13:00" }, new { start = "12:30", end = "14:00" } } }),
            ct);
        (await overlap.ProblemCodeAsync(ct)).ShouldBe("schedule.break.overlap");
    }

    [Fact]
    public async Task A_rotating_two_on_two_off_pattern_is_saved()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonWithOneMasterAsync(ct);
        var shift = new { start = "10:00", end = "20:00" };

        var response = await salon.Owner.PutAsJsonAsync(
            $"/api/masters/{salon.Master}/schedule/pattern",
            new { pattern = new { kind = "rotating", anchor = "2026-11-01", cycle = new object?[] { shift, shift, null, null } } },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var pattern = (await JsonAsync(response, ct)).GetProperty("pattern");
        pattern.GetProperty("kind").GetString().ShouldBe("rotating");
        pattern.GetProperty("cycle").GetArrayLength().ShouldBe(4);
    }

    [Fact]
    public async Task Availability_follows_the_schedule_and_the_masters_duration()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonWithOneMasterAsync(ct);
        await salon.Owner.PutAsJsonAsync($"/api/masters/{salon.Master}/schedule/pattern", EveryDay(TenToTwo), ct);

        var publicSite = factory.CreateClientFor($"{salon.Slug}.{ApiWithDatabaseFactory.RootDomain}");
        var response = await publicSite.GetAsync($"/api/availability?serviceId={salon.Service}&date={DayString}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
        var body = await JsonAsync(response, ct);
        body.GetProperty("timeZone").GetString().ShouldBe("Asia/Tbilisi");
        var times = body.GetProperty("slots").EnumerateArray().Select(s => s.GetProperty("time").GetString()).ToList();
        times.ShouldBe(["10:00", "10:15", "10:30", "10:45", "11:00", "13:00"]);
        var option = body.GetProperty("slots")[0].GetProperty("masters").EnumerateArray().ShouldHaveSingleItem();
        option.GetProperty("masterName").GetString().ShouldBe("Luka");
        option.GetProperty("durationMinutes").GetInt32().ShouldBe(60);
    }

    [Fact]
    public async Task A_day_off_removes_all_slots_and_masters_can_add_their_own()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonWithOneMasterAsync(ct);
        await salon.Owner.PutAsJsonAsync($"/api/masters/{salon.Master}/schedule/pattern", EveryDay(TenToTwo), ct);

        // Invite the master and let them accept, so they have an account linked to the profile.
        var email = $"luka-{Guid.NewGuid():N}@example.ge";
        await salon.Owner.PostAsJsonAsync("/api/invitations", new { email, role = "Master", masterId = salon.Master }, ct);
        var body = factory.Emails.LastTo(email).TextBody;
        var token = Uri.UnescapeDataString(body[(body.IndexOf("token=", StringComparison.Ordinal) + 6)..].Split('\n')[0].Trim());
        var master = factory.CreatePlatformClient();
        (await master.PostAsJsonAsync("/api/invitations/accept", new { token, displayName = "Luka", password = ApiWithDatabaseFactory.Password }, ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        master.DefaultRequestHeaders.Add("X-Mastera-Tenant", salon.Slug);

        var mine = await JsonAsync(await master.GetAsync("/api/masters/me", ct), ct);
        mine.GetProperty("id").GetGuid().ShouldBe(salon.Master);

        var dayOff = await master.PostAsJsonAsync($"/api/masters/{salon.Master}/schedule/exceptions", new { from = DayString, note = "Doctor" }, ct);
        dayOff.StatusCode.ShouldBe(HttpStatusCode.Created);

        var slots = await JsonAsync(await master.GetAsync($"/api/availability?serviceId={salon.Service}&date={DayString}", ct), ct);
        slots.GetProperty("slots").GetArrayLength().ShouldBe(0);

        var notAllowed = await master.PutAsJsonAsync($"/api/masters/{salon.Master}/schedule/pattern", EveryDay(TenToTwo), ct);
        notAllowed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var exceptionId = (await JsonAsync(dayOff, ct)).GetProperty("id").GetGuid();
        (await master.DeleteAsync($"/api/masters/{salon.Master}/schedule/exceptions/{exceptionId}", ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var back = await JsonAsync(await master.GetAsync($"/api/availability?serviceId={salon.Service}&date={DayString}", ct), ct);
        back.GetProperty("slots").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Another_master_cannot_change_your_days_off()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = await factory.CreateUserAsync(MembershipRole.Master);
        var client = factory.CreatePlatformClient();
        await client.LoginAsync(email, ApiWithDatabaseFactory.Password, ct);
        client.DefaultRequestHeaders.Add("X-Mastera-Tenant", ApiWithDatabaseFactory.SeededSlug);

        var owner = factory.CreatePlatformClient();
        var ownerEmail = await factory.CreateUserAsync(MembershipRole.Owner);
        await owner.LoginAsync(ownerEmail, ApiWithDatabaseFactory.Password, ct);
        owner.DefaultRequestHeaders.Add("X-Mastera-Tenant", ApiWithDatabaseFactory.SeededSlug);
        var otherMaster = await CreateAsync(owner, "/api/masters", new { displayName = "Someone else" }, ct);

        var response = await client.PostAsJsonAsync($"/api/masters/{otherMaster}/schedule/exceptions", new { from = DayString }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ProblemCodeAsync(ct)).ShouldBe("schedule.forbidden");
    }

    [Fact]
    public async Task Any_master_lists_every_free_master_and_a_specific_master_can_be_asked_for()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonWithOneMasterAsync(ct, durationMinutes: 30);
        var ana = await CreateAsync(salon.Owner, "/api/masters", new { displayName = "Ana" }, ct);
        await salon.Owner.PutAsJsonAsync($"/api/masters/{ana}/services", new { services = new[] { new { serviceId = salon.Service, durationMinutes = 45 } } }, ct);
        foreach (var id in new[] { salon.Master, ana })
        {
            await salon.Owner.PutAsJsonAsync($"/api/masters/{id}/schedule/pattern", EveryDay(TenToTwo), ct);
        }

        var any = await JsonAsync(await salon.Owner.GetAsync($"/api/availability?serviceId={salon.Service}&date={DayString}", ct), ct);
        var first = any.GetProperty("slots")[0];
        first.GetProperty("time").GetString().ShouldBe("10:00");
        first.GetProperty("masters").GetArrayLength().ShouldBe(2);

        var onlyAna = await JsonAsync(await salon.Owner.GetAsync($"/api/availability?serviceId={salon.Service}&masterId={ana}&date={DayString}", ct), ct);
        onlyAna.GetProperty("slots").EnumerateArray()
            .SelectMany(s => s.GetProperty("masters").EnumerateArray())
            .ShouldAllBe(m => m.GetProperty("masterName").GetString() == "Ana" && m.GetProperty("durationMinutes").GetInt32() == 45);
    }

    [Fact]
    public async Task Availability_needs_a_salon_a_valid_date_and_a_known_service()
    {
        var ct = TestContext.Current.CancellationToken;
        var salon = await SalonWithOneMasterAsync(ct);
        var publicSite = factory.CreateClientFor($"{salon.Slug}.{ApiWithDatabaseFactory.RootDomain}");

        (await (await factory.CreatePlatformClient().GetAsync($"/api/availability?serviceId={salon.Service}&date={DayString}", ct)).ProblemCodeAsync(ct))
            .ShouldBe("tenant.none");
        (await (await publicSite.GetAsync($"/api/availability?serviceId={salon.Service}&date=tomorrow", ct)).ProblemCodeAsync(ct))
            .ShouldBe("schedule.date.invalid");
        (await (await publicSite.GetAsync($"/api/availability?serviceId={Guid.NewGuid()}&date={DayString}", ct)).ProblemCodeAsync(ct))
            .ShouldBe("service.not_found");
    }
}
