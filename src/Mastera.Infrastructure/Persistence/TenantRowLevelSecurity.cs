using Mastera.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mastera.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL row-level security for tenant-owned tables: a second safety layer under the EF Core query filter.
/// Applied automatically to every <see cref="ITenantOwned"/> table after migrations run (EF Core seeding hook),
/// so new tables are covered without touching generated migrations. The SQL is idempotent.
/// Policies do not apply to superusers, so the application must connect as a normal role (see docs/architecture.md).
/// </summary>
public static class TenantRowLevelSecurity
{
    public const string PolicyName = "tenant_isolation";

    public static string EnableSql(string table, string tenantColumn = "TenantId")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantColumn);

        var predicate = $"\"{tenantColumn}\" = NULLIF(current_setting('{TenantConnectionInterceptor.SettingName}', true), '')::uuid";

        return $"""
            ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS {PolicyName} ON "{table}";
            CREATE POLICY {PolicyName} ON "{table}" USING ({predicate}) WITH CHECK ({predicate});
            """;
    }

    public static void EnableTenantRowLevelSecurity(this MigrationBuilder migrationBuilder, string table, string tenantColumn = "TenantId")
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(EnableSql(table, tenantColumn));
    }

    /// <summary>The tables of all tenant-owned entity types in the model.</summary>
    public static IReadOnlyList<string> TenantOwnedTables(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.GetEntityTypes()
            .Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType) && !t.IsOwned())
            .Select(t => t.GetTableName())
            .OfType<string>()
            .Distinct()
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Enables (or re-applies) the policy on every tenant-owned table.</summary>
    public static string EnsureAllSql(IModel model) =>
        string.Concat(TenantOwnedTables(model).Select(table => EnableSql(table)));

    public static void EnsureAll(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sql = EnsureAllSql(context.Model);
        if (sql.Length > 0)
        {
            context.Database.ExecuteSqlRaw(sql);
        }
    }

    public static async Task EnsureAllAsync(DbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sql = EnsureAllSql(context.Model);
        if (sql.Length > 0)
        {
            await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }
}
