using Mastera.Domain.Catalog;
using Mastera.Domain.Clients;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.Bookings;

/// <summary>
/// Task 5.1: an appointment of a client with a master for a service. Its status moves through a small state machine:
/// Pending → Confirmed → Completed / NoShow, and Pending / Confirmed → Cancelled (by client or salon).
/// </summary>
public sealed class Booking : Entity<BookingId>, ITenantOwned
{
    public const int NoteMaxLength = 500;
    public const int GuestNameMaxLength = 100;

    private Booking(BookingId id)
        : base(id)
    {
    }

    private Booking()
        : base(default)
    {
    }

    public TenantId TenantId { get; private set; }

    public MasterId MasterId { get; private set; }

    public ServiceId ServiceId { get; private set; }

    /// <summary>The client; null for a walk-in who didn't leave a phone number (see <see cref="GuestName"/>).</summary>
    public ClientId? ClientId { get; private set; }

    public string? GuestName { get; private set; }

    public Instant Start { get; private set; }

    public Instant End { get; private set; }

    public BookingStatus Status { get; private set; }

    public BookingSource Source { get; private set; }

    /// <summary>The price quoted when booking (the master's own price if they have one).</summary>
    public decimal Price { get; private set; }

    /// <summary>What the client actually paid, recorded when the booking is completed (task 5.7).</summary>
    public decimal? FinalPrice { get; private set; }

    public string? Note { get; private set; }

    public Instant CreatedAt { get; private init; }

    public Instant? CancelledAt { get; private set; }

    public bool IsActive => Status.IsActive();

    public Duration Length => End - Start;

    public static Booking Create(
        MasterId masterId,
        ServiceId serviceId,
        ClientId? clientId,
        string? guestName,
        Instant start,
        Duration duration,
        decimal price,
        BookingSource source,
        bool confirmed,
        string? note,
        Instant now)
    {
        if (masterId == default || serviceId == default)
        {
            throw new DomainException("booking.invalid", "A booking needs a master and a service.");
        }

        if (duration <= Duration.Zero)
        {
            throw new DomainException("booking.duration.invalid", "A booking must last longer than zero minutes.");
        }

        Service.EnsureValidPrice(price);

        var guest = Text.Optional(guestName, GuestNameMaxLength, "booking.guest.length", "The name");
        if (clientId is null && guest is null)
        {
            throw new DomainException("booking.client.required", "A booking needs a client or at least a name.");
        }

        return new Booking(BookingId.New())
        {
            MasterId = masterId,
            ServiceId = serviceId,
            ClientId = clientId,
            GuestName = clientId is null ? guest : null,
            Start = start,
            End = start + duration,
            Price = price,
            Source = source,
            Status = confirmed ? BookingStatus.Confirmed : BookingStatus.Pending,
            Note = Text.Optional(note, NoteMaxLength, "booking.note.length", "The note"),
            CreatedAt = now,
        };
    }

    public bool Overlaps(Instant start, Instant end) => Start < end && start < End;

    public void Confirm()
    {
        EnsureStatus(BookingStatus.Pending);
        Status = BookingStatus.Confirmed;
    }

    public void CancelByClient(Instant now) => Cancel(BookingStatus.CancelledByClient, now);

    public void CancelBySalon(Instant now) => Cancel(BookingStatus.CancelledBySalon, now);

    public void Complete(decimal? finalPrice, Instant now)
    {
        EnsureStatus(BookingStatus.Confirmed);
        EnsureStarted(now);
        if (finalPrice is { } price)
        {
            Service.EnsureValidPrice(price);
        }

        FinalPrice = finalPrice ?? Price;
        Status = BookingStatus.Completed;
    }

    public void MarkNoShow(Instant now)
    {
        EnsureStatus(BookingStatus.Confirmed);
        EnsureStarted(now);
        Status = BookingStatus.NoShow;
    }

    private void Cancel(BookingStatus status, Instant now)
    {
        EnsureStatus(BookingStatus.Pending, BookingStatus.Confirmed);
        Status = status;
        CancelledAt = now;
    }

    private void EnsureStarted(Instant now)
    {
        if (now < Start)
        {
            throw new DomainException("booking.not_started", "This can only be recorded once the appointment has started.");
        }
    }

    private void EnsureStatus(params BookingStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException("booking.transition.invalid", $"A booking that is {Status} can't be changed like this.");
        }
    }
}
