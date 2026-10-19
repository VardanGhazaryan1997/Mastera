using Mastera.Domain.Memberships;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Identity;
using Mastera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
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

    public const string Password = "secret123";

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

    /// <summary>A client on the platform domain (no salon from the subdomain), keeping cookies between calls.</summary>
    public HttpClient CreatePlatformClient() => CreateClientFor(RootDomain);

    /// <summary>Creates a user account and, if <paramref name="role"/> is given, makes them a member of the seeded salon.</summary>
    public async Task<string> CreateUserAsync(MembershipRole? role)
    {
        var email = $"user-{Guid.NewGuid():N}@example.ge";

        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Test User" };
        var created = await users.CreateAsync(user, Password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", created.Errors.Select(e => e.Description)));
        }

        if (role is { } r)
        {
            var db = scope.ServiceProvider.GetRequiredService<MasteraDbContext>();
            db.Memberships.Add(Membership.Create(SeededTenantId, user.Id, r, SystemClock.Instance.GetCurrentInstant()));
            await db.SaveChangesAsync();
        }

        return email;
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
