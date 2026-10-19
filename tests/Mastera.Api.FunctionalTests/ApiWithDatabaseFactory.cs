using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Testcontainers.PostgreSql;

namespace Mastera.Api.FunctionalTests;

/// <summary>
/// Hosts the real API against a real PostgreSQL (Testcontainers), migrated with the real migrations.
/// Salons live under the root domain "mastera.test", e.g. http://barber-house.mastera.test.
/// </summary>
public sealed class ApiWithDatabaseFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string RootDomain = "mastera.test";
    public const string SeededSlug = "barber-house";
    public const string SeededName = "Barber House";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public TenantId SeededTenantId { get; private set; }

    public HttpClient CreateClientFor(string host) =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}") });

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasteraDbContext>();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create(
            SeededName,
            TenantSlug.Create(SeededSlug),
            TimeZoneId.Tbilisi,
            Currency.Gel,
            Language.Georgian,
            SystemClock.Instance.GetCurrentInstant());
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        SeededTenantId = tenant.Id;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Mastera", _container.GetConnectionString());
        builder.UseSetting("Tenancy:RootDomain", RootDomain);
    }
}
