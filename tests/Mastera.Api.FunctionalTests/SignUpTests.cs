using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mastera.Api.FunctionalTests;

/// <summary>Task 1.6: a salon signs up.</summary>
[Trait("Category", "Integration")]
public sealed class SignUpTests(ApiWithDatabaseFactory factory) : IClassFixture<ApiWithDatabaseFactory>
{
    private static object SignUp(string slug, string? email = null, string password = "secret123") => new
    {
        salonName = "Nail Studio Vake",
        slug,
        ownerName = "Nino Kapanadze",
        email = email ?? $"owner-{Guid.NewGuid():N}@example.ge",
        password,
    };

    private static string NewSlug() => $"salon-{Guid.NewGuid():N}"[..20];

    [Fact]
    public async Task Sign_up_creates_the_salon_and_signs_the_owner_in()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreatePlatformClient();
        var slug = NewSlug();

        var response = await client.PostAsJsonAsync("/api/signup", SignUp(slug), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using (var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)))
        {
            created.RootElement.GetProperty("slug").GetString().ShouldBe(slug);
        }

        var me = await client.GetAsync("/api/auth/me", ct);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await me.Content.ReadAsStringAsync(ct));
        var salon = body.RootElement.GetProperty("memberships").EnumerateArray().ShouldHaveSingleItem();
        salon.GetProperty("slug").GetString().ShouldBe(slug);
        salon.GetProperty("name").GetString().ShouldBe("Nail Studio Vake");
        salon.GetProperty("role").GetString().ShouldBe("Owner");

        var publicSite = factory.CreateClientFor($"{slug}.{ApiWithDatabaseFactory.RootDomain}");
        (await publicSite.GetAsync("/api/tenant", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_taken_address_returns_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreatePlatformClient();

        var response = await client.PostAsJsonAsync("/api/signup", SignUp(ApiWithDatabaseFactory.SeededSlug), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync(ct)).ShouldBe("tenant.slug.taken");
    }

    [Fact]
    public async Task An_email_that_already_has_an_account_returns_409_and_creates_no_salon()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreatePlatformClient();
        var email = $"owner-{Guid.NewGuid():N}@example.ge";
        (await client.PostAsJsonAsync("/api/signup", SignUp(NewSlug(), email), ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
        var secondSlug = NewSlug();

        var response = await factory.CreatePlatformClient().PostAsJsonAsync("/api/signup", SignUp(secondSlug, email), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync(ct)).ShouldBe("auth.email.taken");
        var publicSite = factory.CreateClientFor($"{secondSlug}.{ApiWithDatabaseFactory.RootDomain}");
        (await publicSite.GetAsync("/api/tenant", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("bad--slug", "tenant.slug.format")]
    [InlineData("api", "tenant.slug.reserved")]
    public async Task An_invalid_address_returns_400(string slug, string code)
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await factory.CreatePlatformClient().PostAsJsonAsync("/api/signup", SignUp(slug), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync(ct)).ShouldBe(code);
    }

    [Fact]
    public async Task A_weak_password_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await factory.CreatePlatformClient().PostAsJsonAsync("/api/signup", SignUp(NewSlug(), password: "short"), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync(ct)).ShouldBe("auth.password.weak");
    }
}
