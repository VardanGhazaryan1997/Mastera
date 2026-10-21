using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mastera.Api.FunctionalTests;

/// <summary>Hosts the real API in memory. Later epics add a Testcontainers database here.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // No database here: keep the notification worker from polling one.
        builder.UseSetting("Notifications:WorkerEnabled", "false");
    }
}
