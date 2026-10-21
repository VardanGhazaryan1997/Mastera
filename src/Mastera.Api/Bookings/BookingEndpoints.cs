using Mastera.Api.Auth;
using Mastera.Api.Common;
using Mastera.Application.Bookings;
using Mastera.Application.Common;
using Mastera.Application.Tenancy;
using Mastera.Application.Users;
using Mastera.Domain.Clients;
using Mastera.Domain.Masters;
using Mastera.Domain.Memberships;
using NodaTime;

namespace Mastera.Api.Bookings;

/// <summary>Tasks 5.3–5.5: holds and online booking (public, rate-limited), staff bookings and blocked time.</summary>
public static class BookingEndpoints
{
    public const string PublicRateLimit = "public-booking";

    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Public booking flow on the salon's own address: hold a slot, then book it.
        app.MapPost("/api/holds", async (HoldInput input, BookingHandler handler, CancellationToken ct) =>
            (await handler.HoldAsync(input, ct)).ToCreated(h => $"/api/holds/{h.Id}"))
            .AllowAnonymous().RequireRateLimiting(PublicRateLimit).WithTags("Booking");

        app.MapPost("/api/bookings", async (OnlineBookingInput input, BookingHandler handler, CancellationToken ct) =>
            (await handler.BookOnlineAsync(input, ct)).ToCreated(b => $"/api/bookings/{b.Id}"))
            .AllowAnonymous().RequireRateLimiting(PublicRateLimit).WithTags("Booking");

        // Dashboard: staff enter bookings and see the calendar.
        app.MapPost("/api/bookings/manual", async (ManualBookingInput input, BookingHandler handler, CancellationToken ct) =>
            (await handler.BookManualAsync(input, ct)).ToCreated(b => $"/api/bookings/{b.Id}"))
            .RequireAuthorization(Policies.SalonDesk).WithTags("Booking");

        app.MapGet("/api/calendar", async (
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? masterId,
            ICurrentUser user,
            IBookingRepository bookings,
            Mastera.Application.Masters.IMasterRepository masters,
            ITenantContext tenant,
            ITenantSettings settings,
            CancellationToken ct) =>
        {
            if (to <= from || to - from > TimeSpan.FromDays(42))
            {
                return ProblemResults.Problem(StatusCodes.Status400BadRequest, "Choose a range of at most 6 weeks.", "calendar.range.invalid");
            }

            // Masters see only their own calendar.
            MasterId? filter = masterId is { } id ? new MasterId(id) : null;
            if (user.Role == MembershipRole.Master)
            {
                var own = user.UserId is { } userId ? await masters.FindByUserAsync(userId, ct) : null;
                if (own is null)
                {
                    return Results.Ok(new CalendarView([], []));
                }

                filter = own.Id;
            }

            var calendar = await bookings.ListAsync(Instant.FromDateTimeOffset(from), Instant.FromDateTimeOffset(to), filter, ct);

            // Task 5.8: the salon can hide client phone numbers from masters.
            if (user.Role == MembershipRole.Master
                && (await settings.GetBookingSettingsAsync(tenant.Current!.Id, ct)).HideClientPhoneFromMasters)
            {
                calendar = calendar with
                {
                    Bookings = calendar.Bookings
                        .Select(b => b with { ClientPhone = b.ClientPhone is { } phone ? PhoneNumber.Create(phone).Masked() : null })
                        .ToList(),
                };
            }

            return Results.Ok(calendar);
        }).RequireAuthorization(Policies.SalonMember).WithTags("Booking");

        // Task 5.7: staff change a booking's status (masters only their own bookings).
        app.MapPost("/api/bookings/{id:guid}/confirm", async (Guid id, BookingHandler handler, CancellationToken ct) =>
            (await handler.ConfirmAsync(id, ct)).ToHttp()).RequireAuthorization(Policies.SalonMember).WithTags("Booking");

        app.MapPost("/api/bookings/{id:guid}/cancel", async (Guid id, BookingHandler handler, CancellationToken ct) =>
            (await handler.CancelAsync(id, ct)).ToHttp()).RequireAuthorization(Policies.SalonMember).WithTags("Booking");

        app.MapPost("/api/bookings/{id:guid}/complete", async (Guid id, CompleteBookingInput? input, BookingHandler handler, CancellationToken ct) =>
            (await handler.CompleteAsync(id, input ?? new CompleteBookingInput(null), ct)).ToHttp()).RequireAuthorization(Policies.SalonMember).WithTags("Booking");

        app.MapPost("/api/bookings/{id:guid}/no-show", async (Guid id, BookingHandler handler, CancellationToken ct) =>
            (await handler.NoShowAsync(id, ct)).ToHttp()).RequireAuthorization(Policies.SalonMember).WithTags("Booking");

        // Task 5.6: the client's own link (from the SMS), on the salon's address, no sign-in.
        app.MapGet("/api/manage/{token}", async (string token, ClientBookingHandler handler, CancellationToken ct) =>
            (await handler.GetAsync(token, ct)).ToHttp())
            .AllowAnonymous().RequireRateLimiting(PublicRateLimit).WithTags("Booking");

        app.MapPost("/api/manage/{token}/cancel", async (string token, ClientBookingHandler handler, CancellationToken ct) =>
            (await handler.CancelAsync(token, ct)).ToHttp())
            .AllowAnonymous().RequireRateLimiting(PublicRateLimit).WithTags("Booking");

        app.MapPost("/api/manage/{token}/reschedule", async (string token, RescheduleInput input, ClientBookingHandler handler, CancellationToken ct) =>
            (await handler.RescheduleAsync(token, input, ct)).ToHttp())
            .AllowAnonymous().RequireRateLimiting(PublicRateLimit).WithTags("Booking");

        app.MapPost("/api/blocked-times", async (BlockTimeInput input, BookingHandler handler, CancellationToken ct) =>
            (await handler.BlockAsync(input, ct)).ToCreated(b => $"/api/blocked-times/{b.Id}"))
            .RequireAuthorization(Policies.SalonMember).WithTags("Booking");

        app.MapDelete("/api/blocked-times/{id:guid}", async (Guid id, BookingHandler handler, CancellationToken ct) =>
        {
            var result = await handler.UnblockAsync(id, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).RequireAuthorization(Policies.SalonMember).WithTags("Booking");

        return app;
    }
}
