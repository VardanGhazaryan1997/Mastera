using Mastera.Application.Tenancy;
using Mastera.Infrastructure.Persistence;
using Mastera.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mastera.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Mastera";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The connection string is read when the DbContext is first built, so test hosts can override it.
        services.AddDbContext<MasteraDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is missing. Set ConnectionStrings:{ConnectionStringName}.");
            options.UseMastera(connectionString);
        });

        services.AddScoped<ITenantLookup, TenantLookup>();

        return services;
    }
}
