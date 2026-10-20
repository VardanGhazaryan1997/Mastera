using Mastera.Application.Availability;
using Mastera.Application.Catalog;
using Mastera.Application.Clients;
using Mastera.Application.Common;
using Mastera.Application.Masters;
using Mastera.Application.Tenancy;
using Mastera.Application.Users;
using Mastera.Domain.Bookings;
using Mastera.Domain.Catalog;
using Mastera.Domain.Clients;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Memberships;
using NodaTime;

namespace Mastera.Application.Bookings;

public sealed record HoldInput(Guid ServiceId, Guid? MasterId, DateTimeOffset Start);

public sealed record HoldView(Guid Id, Guid MasterId, string MasterName, Guid ServiceId, DateTimeOffset Start, DateTimeOffset End, decimal Price, DateTimeOffset ExpiresAt);

public sealed record OnlineBookingInput(Guid HoldId, string? Name, string? Phone, string? Email, string? Note);

public sealed record ManualBookingInput(
    Guid MasterId,
    Guid ServiceId,
    DateTimeOffset Start,
    string? ClientName,
    string? ClientPhone,
    bool WalkIn = false,
    string? Note = null,
    int? DurationMinutes = null,
    decimal? Price = null);

public sealed record BlockTimeInput(Guid MasterId, DateTimeOffset Start, DateTimeOffset End, string? Reason);

public sealed record BookingCreated(Guid Id, string Status, Guid MasterId, string MasterName, Guid ServiceId, string ServiceName, DateTimeOffset Start, DateTimeOffset End, decimal Price);

/// <summary>Tasks 5.3–5.5: holding a slot, booking online from a hold, and staff entering bookings and blocked time.</summary>
public sealed class BookingHandler(
    IBookingRepository bookings,
    IClientRepository clients,
    ICatalogRepository catalog,
    IMasterRepository masters,
    AvailabilityQuery availability,
    ITenantSettings tenantSettings,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const string SlotTakenCode = "booking.slot_taken";

    /// <summary>Task 5.3: reserve a free slot for 10 minutes while the client enters their details.</summary>
    public async Task<Result<HoldView>> HoldAsync(HoldInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (tenantContext.Current is not { } salon)
        {
            return Result.Failure<HoldView>(Errors.NoTenant());
        }

        var start = Instant.FromDateTimeOffset(input.Start);
        var zone = await tenantSettings.GetTimeZoneAsync(salon.Id, ct);
        var date = Schedules.ScheduleMapping.Format(start.InZone(zone).Date);

        var free = await availability.GetAsync(input.ServiceId, input.MasterId, date, ct);
        if (!free.IsSuccess)
        {
            return Result.Failure<HoldView>(free.Error!);
        }

        var slot = free.Value!.Slots.FirstOrDefault(s => s.Start == input.Start.ToUniversalTime());
        var option = slot?.Masters.FirstOrDefault();
        if (slot is null || option is null)
        {
            return Result.Failure<HoldView>(SlotTaken());
        }

        var now = clock.GetCurrentInstant();
        var masterId = new MasterId(option.MasterId);
        await bookings.RemoveExpiredHoldsAsync(masterId, now, ct);

        var hold = SlotHold.Create(masterId, new ServiceId(input.ServiceId), start, Duration.FromMinutes(option.DurationMinutes), option.Price, now);
        bookings.Add(hold);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (SlotTakenException)
        {
            return Result.Failure<HoldView>(SlotTaken());
        }

        return Result.Success(new HoldView(
            hold.Id.Value, option.MasterId, option.MasterName, input.ServiceId,
            hold.Start.ToDateTimeOffset(), hold.End.ToDateTimeOffset(), hold.Price, hold.ExpiresAt.ToDateTimeOffset()));
    }

    /// <summary>Task 5.4: the client confirms a held slot with their name and phone.</summary>
    public async Task<Result<BookingCreated>> BookOnlineAsync(OnlineBookingInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var hold = await bookings.FindHoldAsync(new SlotHoldId(input.HoldId), ct);
        var now = clock.GetCurrentInstant();
        if (hold is null || hold.IsExpired(now))
        {
            return Result.Failure<BookingCreated>(Error.Validation(
                "booking.hold.expired", "The time you picked is no longer reserved. Choose a time again."));
        }

        PhoneNumber phone;
        try
        {
            phone = PhoneNumber.Create(input.Phone);
        }
        catch (DomainException e)
        {
            return Result.Failure<BookingCreated>(Errors.FromDomain(e));
        }

        var client = await clients.FindByPhoneAsync(phone, ct);
        try
        {
            if (client is null)
            {
                client = Client.Create(input.Name, phone, now);
                client.ChangeEmail(input.Email);
                clients.Add(client);
            }
            else if (!string.IsNullOrWhiteSpace(input.Email) && client.Email is null)
            {
                client.ChangeEmail(input.Email);
            }
        }
        catch (DomainException e)
        {
            return Result.Failure<BookingCreated>(Errors.FromDomain(e));
        }

        Booking booking;
        try
        {
            booking = Booking.Create(
                hold.MasterId, hold.ServiceId, client.Id, null, hold.Start, hold.End - hold.Start, hold.Price,
                BookingSource.Online, confirmed: true, input.Note, now);
        }
        catch (DomainException e)
        {
            return Result.Failure<BookingCreated>(Errors.FromDomain(e));
        }

        bookings.Remove(hold);
        bookings.Add(booking);
        return await SaveAsync(booking, ct);
    }

    /// <summary>
    /// Task 5.5: staff enter a booking (phone call, Instagram message) or a walk-in. Staff may book outside the
    /// online rules (notice, horizon, working hours), but never over another booking, hold or blocked time.
    /// </summary>
    public async Task<Result<BookingCreated>> BookManualAsync(ManualBookingInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var master = await masters.FindAsync(new MasterId(input.MasterId), ct);
        var service = await catalog.FindServiceAsync(new ServiceId(input.ServiceId), ct);
        if (master is null || service is null || service.IsArchived)
        {
            return Result.Failure<BookingCreated>(Error.NotFound("booking.target.not_found", "The master or service was not found."));
        }

        var offer = master.Services.FirstOrDefault(s => s.ServiceId == service.Id);
        if (offer is null)
        {
            return Result.Failure<BookingCreated>(Error.Validation("booking.service.not_offered", $"{master.DisplayName} doesn't offer {service.Name}."));
        }

        var now = clock.GetCurrentInstant();
        var start = Instant.FromDateTimeOffset(input.Start);
        Duration duration;
        try
        {
            duration = input.DurationMinutes is { } minutes ? Duration.FromMinutes(minutes) : offer.EffectiveDuration(service);
            Service.EnsureValidDuration(duration);
        }
        catch (DomainException e)
        {
            return Result.Failure<BookingCreated>(Errors.FromDomain(e));
        }

        if (await bookings.IsBusyAsync(master.Id, start, start + duration, now, null, ct))
        {
            return Result.Failure<BookingCreated>(SlotTaken());
        }

        Client? client = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(input.ClientPhone))
            {
                var phone = PhoneNumber.Create(input.ClientPhone);
                client = await clients.FindByPhoneAsync(phone, ct);
                if (client is null)
                {
                    client = Client.Create(input.ClientName, phone, now);
                    clients.Add(client);
                }
            }
            else if (!input.WalkIn)
            {
                return Result.Failure<BookingCreated>(Error.Validation("client.phone.invalid", "Enter the client's phone number."));
            }

            var booking = Booking.Create(
                master.Id, service.Id, client?.Id, client is null ? input.ClientName : null, start, duration,
                input.Price ?? offer.EffectivePrice(service),
                input.WalkIn ? BookingSource.WalkIn : BookingSource.Manual,
                confirmed: true, input.Note, now);

            bookings.Add(booking);
            return await SaveAsync(booking, ct);
        }
        catch (DomainException e)
        {
            return Result.Failure<BookingCreated>(Errors.FromDomain(e));
        }
    }

    /// <summary>Task 5.5: lunch, a personal errand — time nobody can book.</summary>
    public async Task<Result<BlockedTimeView>> BlockAsync(BlockTimeInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var master = await masters.FindAsync(new MasterId(input.MasterId), ct);
        if (master is null)
        {
            return Result.Failure<BlockedTimeView>(Error.NotFound("master.not_found", "The master was not found."));
        }

        if (!CanManageCalendarOf(master))
        {
            return Result.Failure<BlockedTimeView>(Forbidden());
        }

        var now = clock.GetCurrentInstant();
        BlockedTime blocked;
        try
        {
            blocked = BlockedTime.Create(master.Id, Instant.FromDateTimeOffset(input.Start), Instant.FromDateTimeOffset(input.End), input.Reason, now);
        }
        catch (DomainException e)
        {
            return Result.Failure<BlockedTimeView>(Errors.FromDomain(e));
        }

        if (await bookings.IsBusyAsync(master.Id, blocked.Start, blocked.End, now, null, ct))
        {
            return Result.Failure<BlockedTimeView>(Error.Conflict("blocked.overlaps", "There is already a booking or blocked time then. Move or cancel it first."));
        }

        bookings.Add(blocked);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new BlockedTimeView(blocked.Id.Value, master.Id.Value, blocked.Start.ToDateTimeOffset(), blocked.End.ToDateTimeOffset(), blocked.Reason));
    }

    public async Task<Result<bool>> UnblockAsync(Guid id, CancellationToken ct)
    {
        var blocked = await bookings.FindBlockedAsync(new BlockedTimeId(id), ct);
        if (blocked is null)
        {
            return Result.Failure<bool>(Error.NotFound("blocked.not_found", "The blocked time was not found."));
        }

        var master = await masters.FindAsync(blocked.MasterId, ct);
        if (master is null || !CanManageCalendarOf(master))
        {
            return Result.Failure<bool>(Forbidden());
        }

        bookings.Remove(blocked);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    private async Task<Result<BookingCreated>> SaveAsync(Booking booking, CancellationToken ct)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (SlotTakenException)
        {
            return Result.Failure<BookingCreated>(SlotTaken());
        }

        var master = await masters.FindAsync(booking.MasterId, ct);
        var service = await catalog.FindServiceAsync(booking.ServiceId, ct);
        return Result.Success(new BookingCreated(
            booking.Id.Value,
            booking.Status.ToString(),
            booking.MasterId.Value,
            master?.DisplayName ?? string.Empty,
            booking.ServiceId.Value,
            service?.Name ?? string.Empty,
            booking.Start.ToDateTimeOffset(),
            booking.End.ToDateTimeOffset(),
            booking.Price));
    }

    /// <summary>Staff at the desk manage every calendar; a master manages their own.</summary>
    private bool CanManageCalendarOf(Master master) =>
        currentUser.Role is MembershipRole.Owner or MembershipRole.Admin or MembershipRole.Receptionist
        || (currentUser.Role == MembershipRole.Master && currentUser.UserId is { } id && master.UserId == id);

    private static Error SlotTaken() =>
        Error.Conflict(SlotTakenCode, "This time was just taken. Please choose another one.");

    private static Error Forbidden() =>
        new("calendar.forbidden", "You can only change your own calendar.", ErrorKind.Forbidden);
}
