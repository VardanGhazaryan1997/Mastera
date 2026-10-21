using Mastera.Domain.Clients;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.UnitTests.Bookings;

/// <summary>Tasks 5.6, 5.8 and 6.5: salon booking settings, masked phones, Telegram chat on the master.</summary>
public sealed class NotificationRelatedDomainTests
{
    private static Tenant NewTenant() =>
        Tenant.Create("Barber House", TenantSlug.Create("barber-house"), TimeZoneId.Tbilisi, Currency.Gel, Language.Georgian, Instant.FromUtc(2026, 1, 1, 0, 0));

    [Fact]
    public void A_new_salon_shows_phones_and_has_a_two_hour_window()
    {
        var tenant = NewTenant();

        tenant.HideClientPhoneFromMasters.ShouldBeFalse();
        tenant.CancellationWindowHours.ShouldBe(2);
    }

    [Fact]
    public void Booking_settings_can_be_changed_within_limits()
    {
        var tenant = NewTenant();

        tenant.ChangeBookingSettings(hideClientPhoneFromMasters: true, cancellationWindowHours: 0);

        tenant.HideClientPhoneFromMasters.ShouldBeTrue();
        tenant.CancellationWindowHours.ShouldBe(0);
        Should.Throw<DomainException>(() => tenant.ChangeBookingSettings(false, 169)).Code.ShouldBe("tenant.cancellation_window.invalid");
        Should.Throw<DomainException>(() => tenant.ChangeBookingSettings(false, -1)).Code.ShouldBe("tenant.cancellation_window.invalid");
    }

    [Fact]
    public void A_masked_phone_keeps_the_country_code_and_the_last_three_digits()
    {
        PhoneNumber.Create("555 12 34 56").Masked().ShouldBe("+995••••••456");
    }

    [Fact]
    public void A_master_links_and_unlinks_a_Telegram_chat()
    {
        var master = Master.Create("Luka", null, Instant.FromUtc(2026, 1, 1, 0, 0));

        master.LinkTelegram(123456789);
        master.TelegramChatId.ShouldBe(123456789);

        master.UnlinkTelegram();
        master.TelegramChatId.ShouldBeNull();
    }
}
