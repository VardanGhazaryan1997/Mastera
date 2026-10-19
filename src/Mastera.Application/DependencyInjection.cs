using Mastera.Application.Salons;
using Microsoft.Extensions.DependencyInjection;

namespace Mastera.Application;

public static class DependencyInjection
{
    /// <summary>Registers use-case handlers.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<RegisterSalonHandler>();

        return services;
    }
}
