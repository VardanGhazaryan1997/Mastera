using Microsoft.EntityFrameworkCore.Migrations;

namespace Mastera.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL row-level security for tenant-owned tables: a second safety layer under the EF Core query filter.
/// Call <see cref="EnableTenantRowLevelSecurity"/> in the migration that creates each tenant-owned table.
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
            CREATE POLICY {PolicyName} ON "{table}" USING ({predicate}) WITH CHECK ({predicate});
            """;
    }

    public static void EnableTenantRowLevelSecurity(this MigrationBuilder migrationBuilder, string table, string tenantColumn = "TenantId")
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(EnableSql(table, tenantColumn));
    }
}
