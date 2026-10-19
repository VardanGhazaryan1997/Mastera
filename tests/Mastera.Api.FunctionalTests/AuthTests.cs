using System.Net;
using System.Text.Json;
using Mastera.Domain.Memberships;

namespace Mastera.Api.FunctionalTests;

/// <summary>Task 1.5: cookie sign-in, memberships and roles.</summary>
[Trait("Category", "Integration")]
public sealed class AuthTests(ApiWithDatabaseFactory factory) : IClassFixture<ApiWithDatabaseFactory>
{
    [Fact]
    public async Task Signing_in_sets_an_HttpOnly_cookie_and_me_returns_the_user()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = await factory.CreateUserAsync(MembershipRole.Admin);
        var client = factory.CreatePlatformClient();

        var login = await client.LoginAsync(email, ApiWithDatabaseFactory.Password, ct);

        login.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("mastera.auth=", StringComparison.Ordinal));
        cookie.ToLowerInvariant().ShouldContain("httponly");
        cookie.ToLowerInvariant().ShouldContain("samesite=strict");

        var me = await client.GetAsync("/api/auth/me", ct);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await me.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("email").GetString().ShouldBe(email);
        var salon = body.RootElement.GetProperty("memberships").EnumerateArray().ShouldHaveSingleItem();
        salon.GetProperty("slug").GetString().ShouldBe(ApiWithDatabaseFactory.SeededSlug);
        salon.GetProperty("role").GetString().ShouldBe("Admin");
    }

    [Fact]
    public async Task Wrong_password_returns_401_with_a_code()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = await factory.CreateUserAsync(role: null);
        var client = factory.CreatePlatformClient();

        var login = await client.LoginAsync(email, "wrong-password1", ct);

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await login.ProblemCodeAsync(ct)).ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Me_without_signing_in_returns_401_not_a_redirect()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreatePlatformClient();

        var me = await client.GetAsync("/api/auth/me", ct);

        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = await factory.CreateUserAsync(role: null);
        var client = factory.CreatePlatformClient();
        await client.LoginAsync(email, ApiWithDatabaseFactory.Password, ct);

        var logout = await client.PostAsync("/api/auth/logout", content: null, ct);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/auth/me", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(MembershipRole.Owner)]
    [InlineData(MembershipRole.Admin)]
    public async Task Owners_and_admins_can_list_the_salons_members(MembershipRole role)
    {
        var ct = TestContext.Current.CancellationToken;
        var email = await factory.CreateUserAsync(role);
        var client = factory.CreatePlatformClient();
        await client.LoginAsync(email, ApiWithDatabaseFactory.Password, ct);

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/members").ForSalon(ApiWithDatabaseFactory.SeededSlug), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(ct)).ShouldContain(email);
    }

    [Theory]
    [InlineData(MembershipRole.Receptionist)]
    [InlineData(MembershipRole.Master)]
    public async Task Receptionists_and_masters_get_403_on_admin_endpoints(MembershipRole role)
    {
        var ct = TestContext.Current.CancellationToken;
        var email = await factory.CreateUserAsync(role);
        var client = factory.CreatePlatformClient();
        await client.LoginAsync(email, ApiWithDatabaseFactory.Password, ct);

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/members").ForSalon(ApiWithDatabaseFactory.SeededSlug), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_salon_you_dont_work_in_returns_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = await factory.CreateUserAsync(role: null);
        var client = factory.CreatePlatformClient();
        await client.LoginAsync(email, ApiWithDatabaseFactory.Password, ct);

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/members").ForSalon(ApiWithDatabaseFactory.SeededSlug), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ProblemCodeAsync(ct)).ShouldBe("tenant.forbidden");
    }

    [Fact]
    public async Task Admin_endpoints_without_a_salon_return_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = await factory.CreateUserAsync(MembershipRole.Owner);
        var client = factory.CreatePlatformClient();
        await client.LoginAsync(email, ApiWithDatabaseFactory.Password, ct);

        var response = await client.GetAsync("/api/members", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_endpoints_without_signing_in_return_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreatePlatformClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/members").ForSalon(ApiWithDatabaseFactory.SeededSlug), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
