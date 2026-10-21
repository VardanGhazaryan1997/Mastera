using Mastera.Application.Availability;
using Mastera.Application.Catalog;
using Mastera.Application.Common;
using Mastera.Application.Masters;
using Mastera.Application.Notifications;
using Mastera.Application.Tenancy;
using Mastera.Domain.Bookings;
using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Application.Bookings;

public sealed record ManagedBookingView(
    Guid Id,
    string Status,
    Guid ServiceId,
    string ServiceName,
    Guid MasterId,
    string MasterName,
    DateTimeOffset Start,
    DateTimeOffset End,
    decimal Price,
    bool CanChange,
    DateTimeOffset ChangeDeadline);

public sealed record RescheduleInput(DateTimeOffset Start);

/// <summary>
/// Task 5.6: the client opens the link from their SMS to see, cancel or move their booking, up to the salon's
/// cancellation window (2 hours by default). Works on the salon's own address without signing in.
/// </summary>
public sealed class ClientBookingHandler(
    IBookingRepository bookings,
    IMasterRepository masters,
    ICatalogRepository catalog,
    IBookingLinks links,
    AvailabilityQuery availability,
    ITenantContext tenantContext,
    ITenantSettings tenantSettings,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<ManagedBookingView>> GetAsync(string token, CancellationToken ct)
    {
        var (booking, settings, error) = await FindAsync(token, ct);
        return booking is null || settings is null ? Result.Failure<ManagedBookingView>(error!) : await ViewAsync(booking, settings, ct);
    }

    public async Task<Result<ManagedBookingView>> CancelAsync(string token, CancellationToken ct)
    {
        var (booking, settings, error) = await FindAsync(token, ct);
        if (booking is null || settings is null)
        {
            return Result.Failure<ManagedBookingView>(error!);
        }

        var now = clock.GetCurrentInstant();
        try
        {
            BookingPolicy.EnsureClientCanChange(booking, now, settings.CancellationWindow);
            booking.CancelByClient(now);
        }
        catch (DomainException e)
        {
            return Result.Failure<ManagedBookingView>(Errors.FromDomain(e));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return await ViewAsync(booking, settings, ct);
    }

    public async Task<Result<ManagedBookingView>> RescheduleAsync(string token, RescheduleInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (booking, settings, error) = await FindAsync(token, ct);
        if (booking is null || settings is null)
        {
            return Result.Failure<ManagedBookingView>(error!);
        }

        var now = clock.GetCurrentInstant();
        try
        {
            BookingPolicy.EnsureClientCanChange(booking, now, settings.CancellationWindow);
            if (!booking.IsActive)
            {
                throw new DomainException("booking.transition.invalid", "This booking can no longer be changed.");
            }
        }
        catch (DomainException e)
        {
            return Result.Failure<ManagedBookingView>(Errors.FromDomain(e));
        }

        // The new time must be a free slot of the same master (the booking's own time counts as free).
        var start = Instant.FromDateTimeOffset(input.Start);
        var zone = await tenantSettings.GetTimeZoneAsync(tenantContext.Current!.Id, ct);
        var date = Schedules.ScheduleMapping.Format(start.InZone(zone).Date);
        var free = await availability.GetAsync(booking.ServiceId.Value, booking.MasterId.Value, date, booking.Id, ct);
        if (!free.IsSuccess)
        {
            return Result.Failure<ManagedBookingView>(free.Error!);
        }

        var slot = free.Value!.Slots.FirstOrDefault(s => s.Start == input.Start.ToUniversalTime());
        if (slot is null)
        {
            return Result.Failure<ManagedBookingView>(Error.Conflict(BookingHandler.SlotTakenCode, "This time is not free. Please choose another one."));
        }

        booking.Reschedule(start);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (SlotTakenException)
        {
            return Result.Failure<ManagedBookingView>(Error.Conflict(BookingHandler.SlotTakenCode, "This time was just taken. Please choose another one."));
        }

        return await ViewAsync(booking, settings, ct);
    }

    private async Task<(Booking? Booking, BookingSettings? Settings, Error? Error)> FindAsync(string token, CancellationToken ct)
    {
        if (tenantContext.Current is not { } salon)
        {
            return (null, null, Errors.NoTenant());
        }

        var id = links.ReadManageToken(token);
        var booking = id is { } bookingId ? await bookings.FindAsync(bookingId, ct) : null;
        if (booking is null)
        {
            return (null, null, Error.NotFound("booking.link.invalid", "This link is not valid. Please contact the salon."));
        }

        return (booking, await tenantSettings.GetBookingSettingsAsync(salon.Id, ct), null);
    }

    private async Task<Result<ManagedBookingView>> ViewAsync(Booking booking, BookingSettings settings, CancellationToken ct)
    {
        var master = await masters.FindAsync(booking.MasterId, ct);
        var service = await catalog.FindServiceAsync(booking.ServiceId, ct);
        var deadline = booking.Start - settings.CancellationWindow;
        return Result.Success(new ManagedBookingView(
            booking.Id.Value,
            booking.Status.ToString(),
            booking.ServiceId.Value,
            service?.Name ?? string.Empty,
            booking.MasterId.Value,
            master?.DisplayName ?? string.Empty,
            booking.Start.ToDateTimeOffset(),
            booking.End.ToDateTimeOffset(),
            booking.Price,
            booking.IsActive && clock.GetCurrentInstant() <= deadline,
            deadline.ToDateTimeOffset()));
    }
}
