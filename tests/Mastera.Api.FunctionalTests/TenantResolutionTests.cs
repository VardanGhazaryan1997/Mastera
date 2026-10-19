using System.Net;
using System.Text.Json;

namespace Mastera.Api.FunctionalTests;

/// <summary>Task 1.2: the salon is found from the subdomain of every request.</summary>
[Trait("Category", "Integration")]
public sealed class TenantResolutionTests(ApiWithDatabaseFactory factory) : IClassFixture<ApiWithDatabaseFactory>
{
    [Fact]
    public async Task Subdomain_of_a_known_salon_resolves_that_salon()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClientFor($"{ApiWithDatabaseFactory.SeededSlug}.{ApiWithDatabaseFactory.RootDomain}");

        var response = await client.GetAsync("/api/tenant", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("id").GetGuid().ShouldBe(factory.SeededTenantId.Value);
        body.RootElement.GetProperty("slug").GetString().ShouldBe(ApiWithDatabaseFactory.SeededSlug);
        body.RootElement.GetProperty("name").GetString().ShouldBe(ApiWithDatabaseFactory.SeededName);
    }

    [Theory]
    [InlineData("no-such-salon")]
    [InlineData("bad--address")]
    [InlineData("www.barber-house")]
    public async Task Unknown_or_invalid_salon_address_returns_404_ProblemDetails(string subdomain)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClientFor($"{subdomain}.{ApiWithDatabaseFactory.RootDomain}");

        var response = await client.GetAsync("/api/tenant", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("code").GetString().ShouldBe("tenant.not_found");
    }

    [Fact]
    public async Task Unknown_salon_address_returns_404_for_every_path()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClientFor($"no-such-salon.{ApiWithDatabaseFactory.RootDomain}");

        var response = await client.GetAsync("/openapi/v1.json", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Platform_domain_has_no_salon()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClientFor(ApiWithDatabaseFactory.RootDomain);

        var response = await client.GetAsync("/api/tenant", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("code").GetString().ShouldBe("tenant.none");
    }

    [Fact]
    public async Task Platform_domain_still_serves_platform_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClientFor(ApiWithDatabaseFactory.RootDomain);

        var response = await client.GetAsync("/health", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
