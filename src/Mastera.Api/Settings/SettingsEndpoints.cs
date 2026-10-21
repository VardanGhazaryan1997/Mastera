using Mastera.Api.Auth;
using Mastera.Api.Common;
using Mastera.Application.Settings;

namespace Mastera.Api.Settings;

/// <summary>Tasks 5.6/5.8: the salon's booking rules.</summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/settings/booking", async (SalonSettingsHandler handler, CancellationToken ct) =>
            (await handler.GetAsync(ct)).ToHttp())
            .RequireAuthorization(Policies.SalonMember).WithTags("Settings");

        app.MapPut("/api/settings/booking", async (BookingSettingsView input, SalonSettingsHandler handler, CancellationToken ct) =>
            (await handler.UpdateAsync(input, ct)).ToHttp())
            .RequireAuthorization(Policies.SalonAdmin).WithTags("Settings");

        return app;
    }
}
