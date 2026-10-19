using Mastera.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DatabaseConnectivityTests(PostgresFixture db)
{
    [Fact]
    public async Task DbContext_connects_to_a_real_PostgreSQL_17_server()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = db.CreateDbContext();

        (await context.Database.CanConnectAsync(ct)).ShouldBeTrue();

        var version = await context.Database
            .SqlQueryRaw<string>("SELECT version() AS \"Value\"")
            .SingleAsync(ct);
        version.ShouldStartWith("PostgreSQL 17");
    }

    [Fact]
    public async Task DbContext_uses_the_Npgsql_provider()
    {
        await using var context = db.CreateDbContext();

        context.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
    }
}
