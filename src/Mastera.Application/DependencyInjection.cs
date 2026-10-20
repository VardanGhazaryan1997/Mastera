using Mastera.Application.Availability;
using Mastera.Application.Catalog;
using Mastera.Application.Invitations;
using Mastera.Application.Masters;
using Mastera.Application.Salons;
using Mastera.Application.Schedules;
using Microsoft.Extensions.DependencyInjection;

namespace Mastera.Application;

public static class DependencyInjection
{
    /// <summary>Registers use-case handlers.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<RegisterSalonHandler>();
        services.AddScoped<CatalogHandler>();
        services.AddScoped<MasterHandler>();
        services.AddScoped<InvitationHandler>();
        services.AddScoped<ScheduleHandler>();
        services.AddScoped<AvailabilityQuery>();

        return services;
    }
}
