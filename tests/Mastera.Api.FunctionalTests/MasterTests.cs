using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mastera.Api.FunctionalTests;

/// <summary>Tasks 2.2 and 2.3: masters, their services and invitations.</summary>
[Trait("Category", "Integration")]
public sealed partial class MasterTests(ApiWithDatabaseFactory factory) : IClassFixture<ApiWithDatabaseFactory>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response, CancellationToken ct) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();

    private static async Task<Guid> CreateAsync(HttpClient client, string url, object body, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(url, body, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await JsonAsync(response, ct)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Owner_adds_a_master_with_services_and_own_prices()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await factory.SignUpSalonAsync();
        var haircut = await CreateAsync(client, "/api/services", new { name = "Haircut", durationMinutes = 45, price = 35 }, ct);
        var beard = await CreateAsync(client, "/api/services", new { name = "Beard trim", durationMinutes = 20, price = 15 }, ct);
        var luka = await CreateAsync(client, "/api/masters", new { displayName = "Luka", bio = "Fades" }, ct);

        var response = await client.PutAsJsonAsync(
            $"/api/masters/{luka}/services",
            new { services = new object[] { new { serviceId = haircut, price = 50, durationMinutes = 60 }, new { serviceId = beard } } },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var masters = await JsonAsync(await client.GetAsync("/api/masters", ct), ct);
        var master = masters.EnumerateArray().ShouldHaveSingleItem();
        master.GetProperty("displayName").GetString().ShouldBe("Luka");
        var services = master.GetProperty("services").EnumerateArray().ToList();
        services.Count.ShouldBe(2);
        var haircutOffer = services.Single(s => s.GetProperty("serviceId").GetGuid() == haircut);
        haircutOffer.GetProperty("price").GetDecimal().ShouldBe(50m);
        haircutOffer.GetProperty("durationMinutes").GetInt32().ShouldBe(60);
        services.Single(s => s.GetProperty("serviceId").GetGuid() == beard).GetProperty("price").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Changing_the_services_replaces_the_list()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await factory.SignUpSalonAsync();
        var haircut = await CreateAsync(client, "/api/services", new { name = "Haircut", durationMinutes = 45, price = 35 }, ct);
        var beard = await CreateAsync(client, "/api/services", new { name = "Beard trim", durationMinutes = 20, price = 15 }, ct);
        var luka = await CreateAsync(client, "/api/masters", new { displayName = "Luka" }, ct);
        await client.PutAsJsonAsync($"/api/masters/{luka}/services", new { services = new object[] { new { serviceId = haircut }, new { serviceId = beard } } }, ct);

        await client.PutAsJsonAsync($"/api/masters/{luka}/services", new { services = new object[] { new { serviceId = beard } } }, ct);

        var master = (await JsonAsync(await client.GetAsync("/api/masters", ct), ct)).EnumerateArray().Single();
        master.GetProperty("services").EnumerateArray().ShouldHaveSingleItem().GetProperty("serviceId").GetGuid().ShouldBe(beard);
    }

    [Fact]
    public async Task Assigning_another_salons_service_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (salonA, _) = await factory.SignUpSalonAsync();
        var (salonB, _) = await factory.SignUpSalonAsync();
        var foreignService = await CreateAsync(salonA, "/api/services", new { name = "Haircut", durationMinutes = 45, price = 35 }, ct);
        var master = await CreateAsync(salonB, "/api/masters", new { displayName = "Nino" }, ct);

        var response = await salonB.PutAsJsonAsync($"/api/masters/{master}/services", new { services = new object[] { new { serviceId = foreignService } } }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deactivating_a_master_keeps_them_in_the_list()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await factory.SignUpSalonAsync();
        var id = await CreateAsync(client, "/api/masters", new { displayName = "Ana" }, ct);

        var response = await client.PutAsJsonAsync($"/api/masters/{id}", new { displayName = "Ana K.", isActive = false }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var master = (await JsonAsync(await client.GetAsync("/api/masters", ct), ct)).EnumerateArray().Single();
        master.GetProperty("displayName").GetString().ShouldBe("Ana K.");
        master.GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Inviting_a_master_sends_an_email_and_accepting_creates_the_account_membership_and_link()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, slug) = await factory.SignUpSalonAsync();
        var master = await CreateAsync(owner, "/api/masters", new { displayName = "Luka" }, ct);
        var email = $"luka-{Guid.NewGuid():N}@example.ge";

        var invite = await owner.PostAsJsonAsync("/api/invitations", new { email, role = "Master", masterId = master }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var token = TokenFrom(factory.Emails.LastTo(email).TextBody);
        var invitee = factory.CreatePlatformClient();
        var details = await JsonAsync(await invitee.GetAsync($"/api/invitations/{token}", ct), ct);
        details.GetProperty("salonSlug").GetString().ShouldBe(slug);
        details.GetProperty("role").GetString().ShouldBe("Master");
        details.GetProperty("hasAccount").GetBoolean().ShouldBeFalse();

        var accept = await invitee.PostAsJsonAsync("/api/invitations/accept", new { token, displayName = "Luka Barber", password = ApiWithDatabaseFactory.Password }, ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);

        var me = await JsonAsync(await invitee.GetAsync("/api/auth/me", ct), ct);
        var membership = me.GetProperty("memberships").EnumerateArray().ShouldHaveSingleItem();
        membership.GetProperty("slug").GetString().ShouldBe(slug);
        membership.GetProperty("role").GetString().ShouldBe("Master");

        var linked = (await JsonAsync(await owner.GetAsync("/api/masters", ct), ct)).EnumerateArray().Single();
        linked.GetProperty("userId").GetGuid().ShouldBe(me.GetProperty("userId").GetGuid());

        var again = await factory.CreatePlatformClient().PostAsJsonAsync("/api/invitations/accept", new { token, displayName = "X", password = ApiWithDatabaseFactory.Password }, ct);
        (await again.ProblemCodeAsync(ct)).ShouldBe("invitation.used");
    }

    [Fact]
    public async Task Someone_with_an_account_must_sign_in_to_accept()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, slug) = await factory.SignUpSalonAsync();
        var email = await factory.CreateUserAsync(role: null);
        await owner.PostAsJsonAsync("/api/invitations", new { email, role = "Receptionist" }, ct);
        var token = TokenFrom(factory.Emails.LastTo(email).TextBody);

        var anonymous = await factory.CreatePlatformClient().PostAsJsonAsync("/api/invitations/accept", new { token }, ct);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.ProblemCodeAsync(ct)).ShouldBe("invitation.sign_in_required");

        var signedIn = factory.CreatePlatformClient();
        await signedIn.LoginAsync(email, ApiWithDatabaseFactory.Password, ct);
        (await signedIn.PostAsJsonAsync("/api/invitations/accept", new { token }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var me = await JsonAsync(await signedIn.GetAsync("/api/auth/me", ct), ct);
        me.GetProperty("memberships").EnumerateArray().Single(m => m.GetProperty("slug").GetString() == slug)
            .GetProperty("role").GetString().ShouldBe("Receptionist");
    }

    [Fact]
    public async Task An_invitation_for_another_email_cannot_be_accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, _) = await factory.SignUpSalonAsync();
        var invited = $"invited-{Guid.NewGuid():N}@example.ge";
        await owner.PostAsJsonAsync("/api/invitations", new { email = invited, role = "Admin" }, ct);
        var token = TokenFrom(factory.Emails.LastTo(invited).TextBody);

        var someoneElse = factory.CreatePlatformClient();
        await someoneElse.LoginAsync(await factory.CreateUserAsync(role: null), ApiWithDatabaseFactory.Password, ct);
        var response = await someoneElse.PostAsJsonAsync("/api/invitations/accept", new { token }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ProblemCodeAsync(ct)).ShouldBe("invitation.email_mismatch");
    }

    [Fact]
    public async Task Owners_cannot_be_invited_and_bad_links_return_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (owner, _) = await factory.SignUpSalonAsync();

        var asOwner = await owner.PostAsJsonAsync("/api/invitations", new { email = "x@example.ge", role = "Owner" }, ct);
        (await asOwner.ProblemCodeAsync(ct)).ShouldBe("invitation.role.invalid");

        var bad = await factory.CreatePlatformClient().GetAsync("/api/invitations/not-a-real-token", ct);
        bad.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static string TokenFrom(string emailBody)
    {
        var match = InviteLink().Match(emailBody);
        match.Success.ShouldBeTrue($"No invitation link in: {emailBody}");
        return Uri.UnescapeDataString(match.Groups["token"].Value);
    }

    [GeneratedRegex(@"http://dashboard\.test/invite\?token=(?<token>[A-Za-z0-9_%\-]+)")]
    private static partial Regex InviteLink();
}
