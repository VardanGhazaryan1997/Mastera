using Mastera.Api.Auth;
using Mastera.Api.Common;
using Mastera.Application.Bookings;
using Mastera.Application.Common;
using Mastera.Application.Tenancy;
using Mastera.Application.Users;
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

            return Results.Ok(await bookings.ListAsync(Instant.FromDateTimeOffset(from), Instant.FromDateTimeOffset(to), filter, ct));
        }).RequireAuthorization(Policies.SalonMember).WithTags("Booking");

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
