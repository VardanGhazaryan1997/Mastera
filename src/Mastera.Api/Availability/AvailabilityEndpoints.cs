using Mastera.Api.Common;
using Mastera.Application.Availability;

namespace Mastera.Api.Availability;

/// <summary>
/// Task 4.6: free times. Public on the salon's own address (barber-house.mastera.ge/api/availability), and
/// available to the dashboard with the salon header.
/// </summary>
public static class AvailabilityEndpoints
{
    public static IEndpointRouteBuilder MapAvailabilityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/availability", async (Guid serviceId, Guid? masterId, string? date, AvailabilityQuery query, CancellationToken ct) =>
            (await query.GetAsync(serviceId, masterId, date, ct)).ToHttp())
            .AllowAnonymous()
            .WithTags("Availability");

        return app;
    }
}
