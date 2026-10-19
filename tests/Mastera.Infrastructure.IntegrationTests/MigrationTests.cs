using Mastera.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MigrationTests(PostgresFixture db)
{
    [Fact]
    public async Task Migrations_build_an_empty_database_and_match_the_model()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await db.CreateEmptyDatabaseAsync();
        await using var context = db.CreateDbContext(connectionString: connectionString);

        await context.Database.MigrateAsync(ct);

        context.Database.HasPendingModelChanges().ShouldBeFalse(
            "The model has changes that no migration covers. Run: dotnet ef migrations add <Name> -p src/Mastera.Infrastructure -s src/Mastera.Api -o Persistence/Migrations");
        (await context.Database.GetAppliedMigrationsAsync(ct)).ShouldNotBeEmpty();
        (await context.Tenants.CountAsync(ct)).ShouldBe(0);
    }
}
