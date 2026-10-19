using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Mastera.Infrastructure.IntegrationTests.Fixtures;

/// <summary>
/// Starts one PostgreSQL 17 container per test collection and hands out DbContexts bound to it.
/// Shared so the (slow) container start happens once, not per test.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Image = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage(Image)
        .WithDatabase("mastera_tests")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public MasteraDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MasteraDbContext>()
            .UseMastera(ConnectionString)
            .Options;
        return new MasteraDbContext(options);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
