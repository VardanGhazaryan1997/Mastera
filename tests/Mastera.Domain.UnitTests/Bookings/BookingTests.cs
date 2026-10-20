using Mastera.Domain.Bookings;
using Mastera.Domain.Catalog;
using Mastera.Domain.Clients;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using NodaTime;

namespace Mastera.Domain.UnitTests.Bookings;

public sealed class BookingTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 2, 6, 0);
    private static readonly Instant Start = Instant.FromUtc(2026, 11, 2, 8, 0);

    private static Booking New(bool confirmed = true, ClientId? client = null, string? guest = null) =>
        Booking.Create(
            MasterId.New(),
            ServiceId.New(),
            client ?? (guest is null ? ClientId.New() : null),
            guest,
            Start,
            Duration.FromMinutes(45),
            35m,
            BookingSource.Online,
            confirmed,
            note: null,
            Now);

    [Fact]
    public void Create_computes_the_end_from_the_duration()
    {
        var booking = New();

        booking.End.ShouldBe(Start + Duration.FromMinutes(45));
        booking.Price.ShouldBe(35m);
        booking.Status.ShouldBe(BookingStatus.Confirmed);
        booking.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Without_auto_confirm_a_booking_starts_pending()
    {
        New(confirmed: false).Status.ShouldBe(BookingStatus.Pending);
    }

    [Fact]
    public void A_walk_in_without_a_client_needs_a_name()
    {
        New(guest: "Walk-in Giorgi").GuestName.ShouldBe("Walk-in Giorgi");
        Should.Throw<DomainException>(() => Booking.Create(
                MasterId.New(), ServiceId.New(), null, "  ", Start, Duration.FromMinutes(30), 10m, BookingSource.WalkIn, true, null, Now))
            .Code.ShouldBe("booking.client.required");
    }

    [Fact]
    public void Pending_can_be_confirmed_once()
    {
        var booking = New(confirmed: false);

        booking.Confirm();

        booking.Status.ShouldBe(BookingStatus.Confirmed);
        Should.Throw<DomainException>(booking.Confirm).Code.ShouldBe("booking.transition.invalid");
    }

    [Fact]
    public void Cancelling_frees_the_time_and_records_when()
    {
        var booking = New();

        booking.CancelByClient(Now);

        booking.Status.ShouldBe(BookingStatus.CancelledByClient);
        booking.CancelledAt.ShouldBe(Now);
        booking.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void A_cancelled_booking_cannot_be_completed_or_cancelled_again()
    {
        var booking = New();
        booking.CancelBySalon(Now);

        Should.Throw<DomainException>(() => booking.Complete(null, Start)).Code.ShouldBe("booking.transition.invalid");
        Should.Throw<DomainException>(() => booking.CancelByClient(Now)).Code.ShouldBe("booking.transition.invalid");
    }

    [Fact]
    public void Completing_records_the_final_price_or_the_quoted_one()
    {
        var withDiscount = New();
        withDiscount.Complete(30m, Start + Duration.FromMinutes(50));
        withDiscount.Status.ShouldBe(BookingStatus.Completed);
        withDiscount.FinalPrice.ShouldBe(30m);

        var asQuoted = New();
        asQuoted.Complete(null, Start);
        asQuoted.FinalPrice.ShouldBe(35m);
    }

    [Fact]
    public void Completed_and_no_show_need_the_appointment_to_have_started()
    {
        Should.Throw<DomainException>(() => New().Complete(null, Now)).Code.ShouldBe("booking.not_started");
        Should.Throw<DomainException>(() => New().MarkNoShow(Now)).Code.ShouldBe("booking.not_started");
    }

    [Fact]
    public void No_show_is_only_for_confirmed_bookings()
    {
        var booking = New();
        booking.MarkNoShow(Start);
        booking.Status.ShouldBe(BookingStatus.NoShow);

        Should.Throw<DomainException>(() => New(confirmed: false).MarkNoShow(Start)).Code.ShouldBe("booking.transition.invalid");
    }

    [Theory]
    [InlineData(BookingStatus.Pending, true)]
    [InlineData(BookingStatus.Confirmed, true)]
    [InlineData(BookingStatus.Completed, false)]
    [InlineData(BookingStatus.NoShow, false)]
    [InlineData(BookingStatus.CancelledByClient, false)]
    [InlineData(BookingStatus.CancelledBySalon, false)]
    public void Only_pending_and_confirmed_bookings_take_up_time(BookingStatus status, bool active)
    {
        status.IsActive().ShouldBe(active);
    }

    [Fact]
    public void Touching_bookings_do_not_overlap()
    {
        var booking = New();

        booking.Overlaps(booking.End, booking.End + Duration.FromMinutes(30)).ShouldBeFalse();
        booking.Overlaps(Start - Duration.FromMinutes(30), Start).ShouldBeFalse();
        booking.Overlaps(Start + Duration.FromMinutes(10), Start + Duration.FromMinutes(20)).ShouldBeTrue();
    }
}

public sealed class SlotHoldAndBlockedTimeTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 2, 6, 0);

    [Fact]
    public void A_hold_expires_after_ten_minutes()
    {
        var hold = SlotHold.Create(MasterId.New(), ServiceId.New(), Now + Duration.FromHours(3), Duration.FromMinutes(30), 20m, Now);

        hold.End.ShouldBe(hold.Start + Duration.FromMinutes(30));
        hold.IsExpired(Now + Duration.FromMinutes(9)).ShouldBeFalse();
        hold.IsExpired(Now + Duration.FromMinutes(10)).ShouldBeTrue();
    }

    [Fact]
    public void Blocked_time_must_end_after_it_starts_and_last_at_most_a_day()
    {
        var start = Now + Duration.FromHours(5);

        BlockedTime.Create(MasterId.New(), start, start + Duration.FromHours(1), "Lunch", Now).Reason.ShouldBe("Lunch");
        Should.Throw<DomainException>(() => BlockedTime.Create(MasterId.New(), start, start, null, Now)).Code.ShouldBe("blocked.range.invalid");
        Should.Throw<DomainException>(() => BlockedTime.Create(MasterId.New(), start, start + Duration.FromHours(25), null, Now))
            .Code.ShouldBe("blocked.range.invalid");
    }
}
