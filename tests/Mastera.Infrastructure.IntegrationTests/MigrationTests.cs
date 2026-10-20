using Mastera.Infrastructure.IntegrationTests.Fixtures;
using Mastera.Infrastructure.Persistence;
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

    [Fact]
    public async Task After_migrating_every_tenant_owned_table_has_forced_row_level_security()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await db.CreateEmptyDatabaseAsync();
        await using var context = db.CreateDbContext(connectionString: connectionString);
        await context.Database.MigrateAsync(ct);

        var tables = TenantRowLevelSecurity.TenantOwnedTables(context.Model);
        tables.ShouldContain("Services");
        tables.ShouldContain("ServiceCategories");
        tables.ShouldContain("Masters");
        tables.ShouldContain("MasterServices");

        foreach (var table in tables)
        {
            var secured = await context.Database
                .SqlQueryRaw<bool>(
                    "SELECT (c.relrowsecurity AND c.relforcerowsecurity AND EXISTS (SELECT 1 FROM pg_policies p WHERE p.tablename = c.relname AND p.policyname = {1})) AS \"Value\" FROM pg_class c WHERE c.relname = {0}",
                    table,
                    TenantRowLevelSecurity.PolicyName)
                .SingleAsync(ct);
            secured.ShouldBeTrue($"Table {table} has no forced row-level security policy.");
        }
    }

    [Fact]
    public async Task Migrating_twice_is_safe()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await db.CreateEmptyDatabaseAsync();
        await using var context = db.CreateDbContext(connectionString: connectionString);

        await context.Database.MigrateAsync(ct);
        await context.Database.MigrateAsync(ct);
    }
}
