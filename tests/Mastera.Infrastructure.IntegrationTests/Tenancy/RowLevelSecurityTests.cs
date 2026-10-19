using Mastera.Domain.Tenants;
using Mastera.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mastera.Infrastructure.IntegrationTests.Tenancy;

/// <summary>
/// Task 1.4: PostgreSQL row-level security. These tests connect as the normal application role and use raw
/// SQL or IgnoreQueryFilters(), so only the database policy stands between tenants.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RowLevelSecurityTests(PostgresFixture db)
{
    private const string CountNotes = "SELECT count(*)::int AS \"Value\" FROM \"Notes\"";

    [Fact]
    public async Task Without_a_tenant_raw_SQL_returns_no_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.SeedNotesAsync(TenantId.New(), "secret");

        await using var context = db.CreateTestContext(tenant: null, asAppRole: true);

        (await context.Database.SqlQueryRaw<int>(CountNotes).SingleAsync(ct)).ShouldBe(0);
    }

    [Fact]
    public async Task With_a_tenant_raw_SQL_sees_only_that_tenants_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = TenantId.New();
        await db.SeedNotesAsync(a, "a1", "a2");
        await db.SeedNotesAsync(TenantId.New(), "other");

        await using var context = db.CreateTestContext(a, asAppRole: true);

        (await context.Database.SqlQueryRaw<int>(CountNotes).SingleAsync(ct)).ShouldBe(2);
    }

    [Fact]
    public async Task IgnoreQueryFilters_cannot_get_around_the_database_policy()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = TenantId.New();
        await db.SeedNotesAsync(a, "mine");
        await db.SeedNotesAsync(TenantId.New(), "theirs");

        await using var context = db.CreateTestContext(a, asAppRole: true);
        var texts = await context.Notes.IgnoreQueryFilters().Select(n => n.Text).ToListAsync(ct);

        texts.ShouldBe(["mine"]);
    }

    [Fact]
    public async Task Inserting_another_tenants_row_with_raw_SQL_is_rejected_by_the_database()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = db.CreateTestContext(TenantId.New(), asAppRole: true);

        var error = await Should.ThrowAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Notes\" (\"Id\", \"TenantId\", \"Text\") VALUES ({0}, {1}, 'sneaky')",
            [Guid.NewGuid(), TenantId.New().Value],
            ct));

        error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task A_pooled_connection_does_not_keep_the_previous_tenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = TenantId.New();
        await db.SeedNotesAsync(a, "a1");

        await using (var first = db.CreateTestContext(a, asAppRole: true))
        {
            (await first.Database.SqlQueryRaw<int>(CountNotes).SingleAsync(ct)).ShouldBe(1);
        }

        await using var second = db.CreateTestContext(tenant: null, asAppRole: true);
        (await second.Database.SqlQueryRaw<int>(CountNotes).SingleAsync(ct)).ShouldBe(0);
    }

    [Fact]
    public void Policy_SQL_enables_and_forces_row_level_security()
    {
        var sql = Mastera.Infrastructure.Persistence.TenantRowLevelSecurity.EnableSql("Services");

        sql.ShouldContain("ALTER TABLE \"Services\" ENABLE ROW LEVEL SECURITY");
        sql.ShouldContain("ALTER TABLE \"Services\" FORCE ROW LEVEL SECURITY");
        sql.ShouldContain("current_setting('app.tenant_id', true)");
    }
}
