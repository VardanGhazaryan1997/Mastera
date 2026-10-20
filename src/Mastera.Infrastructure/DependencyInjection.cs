using Mastera.Application.Availability;
using Mastera.Application.Bookings;
using Mastera.Application.Catalog;
using Mastera.Application.Clients;
using Mastera.Application.Common;
using Mastera.Application.Email;
using Mastera.Application.Invitations;
using Mastera.Application.Masters;
using Mastera.Application.Memberships;
using Mastera.Application.Salons;
using Mastera.Application.Schedules;
using Mastera.Application.Tenancy;
using Mastera.Application.Users;
using Mastera.Infrastructure.Email;
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
        services.AddScoped<ITenantLookupById, TenantLookup>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IMasterRepository, MasterRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<ITenantSettings, TenantLookup>();
        services.AddScoped<IBusyTimeSource, BookingBusyTimeSource>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();

        // EmailOptions and AppLinkOptions are bound to configuration by the host (see Program.cs).
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IInvitationLinks, InvitationLinks>();

        return services;
    }
}
