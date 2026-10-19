using Mastera.Application.Common;
using Mastera.Application.Memberships;
using Mastera.Application.Salons;
using Mastera.Application.Tenancy;
using Mastera.Application.Users;
using Mastera.Infrastructure.Identity;
using Mastera.Infrastructure.Persistence;
using Mastera.Infrastructure.Repositories;
using Mastera.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

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

        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddScoped<ITenantLookup, TenantLookup>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserAccounts, UserAccounts>();

        return services;
    }
}
