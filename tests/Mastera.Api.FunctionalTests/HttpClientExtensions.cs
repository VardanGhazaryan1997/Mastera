using System.Net.Http.Json;
using System.Text.Json;

namespace Mastera.Api.FunctionalTests;

internal static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password, CancellationToken ct) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password }, ct);

    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response, CancellationToken ct)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return body.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static HttpRequestMessage ForSalon(this HttpRequestMessage request, string slug)
    {
        request.Headers.Add("X-Mastera-Tenant", slug);
        return request;
    }
}
