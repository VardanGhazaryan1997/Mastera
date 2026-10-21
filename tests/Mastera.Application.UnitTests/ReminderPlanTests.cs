using Mastera.Application.Notifications;
using NodaTime;

namespace Mastera.Application.UnitTests;

/// <summary>Task 6.6: reminders 24 h and 2 h before, never in the past.</summary>
public sealed class ReminderPlanTests
{
    private static readonly Instant Start = Instant.FromUtc(2026, 11, 10, 10, 0);

    [Fact]
    public void A_booking_far_ahead_gets_both_reminders()
    {
        var plan = BookingNotificationHandler.PlanReminders(Start, Start - Duration.FromDays(3));

        plan.ShouldBe([
            new PlannedReminder(ReminderKind.DayBefore, Start - Duration.FromHours(24)),
            new PlannedReminder(ReminderKind.TwoHoursBefore, Start - Duration.FromHours(2)),
        ]);
    }

    [Fact]
    public void A_booking_for_later_today_gets_only_the_two_hour_reminder()
    {
        BookingNotificationHandler.PlanReminders(Start, Start - Duration.FromHours(5))
            .ShouldHaveSingleItem().Kind.ShouldBe(ReminderKind.TwoHoursBefore);
    }

    [Fact]
    public void A_booking_within_two_hours_gets_no_reminder()
    {
        BookingNotificationHandler.PlanReminders(Start, Start - Duration.FromMinutes(90)).ShouldBeEmpty();
    }
}
