using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Mastera.Infrastructure.IntegrationTests.Fixtures;

/// <summary>
/// Starts one PostgreSQL 17 container per test collection.
/// The schema is created from <see cref="TestTenantDbContext"/> (the real model plus a test-only tenant-owned
/// "Notes" table with row-level security). A normal, non-superuser role is created as well, because
/// PostgreSQL superusers bypass row-level security.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Image = "postgres:17-alpine";
    public const string AppRole = "mastera_app";
    private const string AppPassword = "mastera_app_password";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .WithDatabase("mastera_tests")
        .Build();

    /// <summary>Superuser connection: row-level security does not apply.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Normal role, like the application uses in production: row-level security applies.</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using (var context = CreateTestContext(tenant: null))
        {
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync(TenantRowLevelSecurity.EnableSql(TestTenantDbContext.NotesTable));
#pragma warning disable EF1002 // Constant role name and password, not user input.
            await context.Database.ExecuteSqlRawAsync($"""
                CREATE ROLE {AppRole} LOGIN PASSWORD '{AppPassword}' NOSUPERUSER NOBYPASSRLS;
                GRANT USAGE ON SCHEMA public TO {AppRole};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {AppRole};
                """);
#pragma warning restore EF1002
        }

        AppConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = AppRole,
            Password = AppPassword,
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>The production DbContext, as the application builds it.</summary>
    public MasteraDbContext CreateDbContext(TenantId? tenant = null, string? connectionString = null)
    {
        var options = new DbContextOptionsBuilder<MasteraDbContext>()
            .UseMastera(connectionString ?? ConnectionString)
            .Options;
        return new MasteraDbContext(options, new FixedTenantContext(tenant));
    }

    /// <summary>A DbContext with the test-only Notes table. <paramref name="asAppRole"/> makes row-level security apply.</summary>
    public TestTenantDbContext CreateTestContext(TenantId? tenant, bool asAppRole = false)
    {
        var options = new DbContextOptionsBuilder<TestTenantDbContext>()
            .UseMastera(asAppRole ? AppConnectionString : ConnectionString)
            .Options;
        return new TestTenantDbContext(options, new FixedTenantContext(tenant));
    }

    /// <summary>Inserts notes for a tenant as the superuser and returns their ids.</summary>
    public async Task<IReadOnlyList<Guid>> SeedNotesAsync(TenantId tenant, params string[] texts)
    {
        await using var context = CreateTestContext(tenant);
        var notes = texts.Select(text => new Note(text)).ToList();
        context.Notes.AddRange(notes);
        await context.SaveChangesAsync();
        return notes.Select(n => n.Id).ToList();
    }

    /// <summary>Creates an empty database next to the test database and returns its connection string.</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = $"empty_{Guid.NewGuid():N}";
        await using (var context = CreateDbContext())
        {
#pragma warning disable EF1002 // Generated identifier, not user input.
            await context.Database.ExecuteSqlRawAsync($"CREATE DATABASE \"{name}\"");
#pragma warning restore EF1002
        }

        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
