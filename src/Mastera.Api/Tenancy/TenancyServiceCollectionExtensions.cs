using Mastera.Application.Tenancy;

namespace Mastera.Api.Tenancy;

public static class TenancyServiceCollectionExtensions
{
    public static IServiceCollection AddTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TenancyOptions>(configuration.GetSection(TenancyOptions.SectionName));
        services.AddScoped<HttpTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<HttpTenantContext>());

        return services;
    }
}
