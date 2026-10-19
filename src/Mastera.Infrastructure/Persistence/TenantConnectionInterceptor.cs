using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mastera.Infrastructure.Persistence;

/// <summary>
/// Every time EF Core opens a connection, sets the PostgreSQL setting <c>app.tenant_id</c> to the current
/// tenant (or to empty when there is none). Row-level security policies read it, so even raw SQL and
/// IgnoreQueryFilters() cannot see another salon's rows. Set on every open, so a pooled connection never
/// carries the previous tenant over.
/// </summary>
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    public const string SettingName = "app.tenant_id";

    public static readonly TenantConnectionInterceptor Instance = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(eventData);

        using var command = CreateCommand(connection, eventData);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(eventData);

        await using var command = CreateCommand(connection, eventData);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand CreateCommand(DbConnection connection, ConnectionEndEventData eventData)
    {
        var tenant = eventData.Context is MasteraDbContext db && db.CurrentTenantId != MasteraDbContext.NoTenant
            ? db.CurrentTenantId.Value.ToString()
            : string.Empty;

        var command = connection.CreateCommand();
        command.CommandText = $"SELECT set_config('{SettingName}', @tenant, false)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        parameter.Value = tenant;
        command.Parameters.Add(parameter);

        return command;
    }
}
