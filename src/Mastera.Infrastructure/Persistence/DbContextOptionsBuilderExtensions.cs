using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Persistence;

public static class DbContextOptionsBuilderExtensions
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>One place that defines how Mastera talks to PostgreSQL (used by the app and by tests).</summary>
    public static DbContextOptionsBuilder UseMastera(this DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNodaTime();
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable);
                npgsql.MigrationsAssembly(typeof(MasteraDbContext).Assembly.GetName().Name);
            })
            // Tenant stamping/checks first, then domain events → outbox rows (task 6.1).
            .AddInterceptors(TenantSaveChangesInterceptor.Instance, Outbox.OutboxInterceptor.Instance, TenantConnectionInterceptor.Instance)
            // Runs after Migrate(), EnsureCreated() and `dotnet ef database update`: row-level security and overlap constraints.
            .UseSeeding((context, _) =>
            {
                TenantRowLevelSecurity.EnsureAll(context);
                BookingConstraints.Ensure(context);
            })
            .UseAsyncSeeding(async (context, _, cancellationToken) =>
            {
                await TenantRowLevelSecurity.EnsureAllAsync(context, cancellationToken);
                await BookingConstraints.EnsureAsync(context, cancellationToken);
            });
    }

    public static DbContextOptionsBuilder<TContext> UseMastera<TContext>(
        this DbContextOptionsBuilder<TContext> builder, string connectionString)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)builder).UseMastera(connectionString);
        return builder;
    }
}
