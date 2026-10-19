using System.Net;
using System.Text.Json;

namespace Mastera.Api.FunctionalTests;

public sealed class PlatformEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_returns_200_and_Healthy()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/health", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(ct)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Unknown_route_returns_404_as_ProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/this-route-does-not-exist", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
    }

    [Fact]
    public async Task OpenApi_document_is_served_in_Development()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/openapi/v1.json", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        body.RootElement.TryGetProperty("openapi", out _).ShouldBeTrue();
    }
}
