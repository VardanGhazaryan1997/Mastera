using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Domain.Bookings;

public sealed record BookingCreatedEvent(BookingId BookingId) : IDomainEvent;

public sealed record BookingConfirmedEvent(BookingId BookingId) : IDomainEvent;

public sealed record BookingCancelledEvent(BookingId BookingId, bool BySalon) : IDomainEvent;

public sealed record BookingRescheduledEvent(BookingId BookingId, Instant PreviousStart) : IDomainEvent;
