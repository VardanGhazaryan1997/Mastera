using Mastera.Domain.Bookings;
using Mastera.Domain.Catalog;
using Mastera.Domain.Clients;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using NodaTime;

namespace Mastera.Domain.UnitTests.Bookings;

/// <summary>Tasks 5.6 and 6.1: domain events, moving a booking, and the client's cancellation window.</summary>
public sealed class BookingEventTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 2, 6, 0);
    private static readonly Instant Start = Instant.FromUtc(2026, 11, 2, 12, 0);

    private static Booking New() =>
        Booking.Create(MasterId.New(), ServiceId.New(), ClientId.New(), null, Start, Duration.FromMinutes(45), 35m, BookingSource.Online, confirmed: false, null, Now);

    [Fact]
    public void Creating_a_booking_raises_BookingCreated()
    {
        var booking = New();

        booking.PeekDomainEvents().ShouldHaveSingleItem().ShouldBe(new BookingCreatedEvent(booking.Id));
    }

    [Fact]
    public void Events_are_kept_until_cleared()
    {
        var booking = New();
        booking.Confirm();

        booking.PeekDomainEvents().Count.ShouldBe(2);
        booking.ClearDomainEvents();
        booking.PeekDomainEvents().ShouldBeEmpty();
    }

    [Fact]
    public void Confirming_and_cancelling_raise_events()
    {
        var booking = New();
        booking.ClearDomainEvents();

        booking.Confirm();
        booking.CancelBySalon(Now);

        booking.PeekDomainEvents().ShouldBe(new IDomainEvent[] { new BookingConfirmedEvent(booking.Id), new BookingCancelledEvent(booking.Id, BySalon: true) });
    }

    [Fact]
    public void Rescheduling_keeps_the_length_and_remembers_the_old_start()
    {
        var booking = New();
        booking.ClearDomainEvents();
        var later = Start + Duration.FromHours(2);

        booking.Reschedule(later);

        booking.Start.ShouldBe(later);
        booking.End.ShouldBe(later + Duration.FromMinutes(45));
        booking.PeekDomainEvents().ShouldHaveSingleItem().ShouldBe(new BookingRescheduledEvent(booking.Id, Start));
    }

    [Fact]
    public void Rescheduling_to_the_same_time_does_nothing()
    {
        var booking = New();
        booking.ClearDomainEvents();

        booking.Reschedule(Start);

        booking.PeekDomainEvents().ShouldBeEmpty();
    }

    [Fact]
    public void A_cancelled_booking_cannot_be_moved()
    {
        var booking = New();
        booking.CancelByClient(Now);

        Should.Throw<DomainException>(() => booking.Reschedule(Start + Duration.FromHours(1))).Code.ShouldBe("booking.transition.invalid");
    }

    [Theory]
    [InlineData(121, true)]
    [InlineData(120, true)]
    [InlineData(119, false)]
    [InlineData(-30, false)]
    public void Clients_may_change_a_booking_until_the_window_starts(int minutesBefore, bool allowed)
    {
        var booking = New();
        var now = Start - Duration.FromMinutes(minutesBefore);

        var change = () => BookingPolicy.EnsureClientCanChange(booking, now, BookingPolicy.DefaultCancellationWindow);

        if (allowed)
        {
            change.ShouldNotThrow();
        }
        else
        {
            Should.Throw<DomainException>(change).Code.ShouldBe("booking.change.too_late");
        }
    }
}
