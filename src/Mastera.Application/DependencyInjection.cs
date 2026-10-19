using Microsoft.Extensions.DependencyInjection;

namespace Mastera.Application;

public static class DependencyInjection
{
    /// <summary>Registers use-case handlers and validators. Filled in from Epic 1 onwards.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
