using Mastera.Domain.Common;
using Mastera.Domain.Tenants;

namespace Mastera.Domain.UnitTests.Tenants;

public sealed class CurrencyTests
{
    [Theory]
    [InlineData("GEL", "GEL")]
    [InlineData(" amd ", "AMD")]
    [InlineData("usd", "USD")]
    public void Create_normalizes_to_uppercase(string raw, string expected)
    {
        Currency.Create(raw).Code.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GE")]
    [InlineData("GELL")]
    [InlineData("G3L")]
    [InlineData("₾")]
    public void Create_rejects_anything_but_three_letters(string? raw)
    {
        Should.Throw<DomainException>(() => Currency.Create(raw)).Code.ShouldBe("tenant.currency.invalid");
    }

    [Fact]
    public void Gel_is_the_Georgian_lari()
    {
        Currency.Gel.Code.ShouldBe("GEL");
    }
}

public sealed class TimeZoneIdTests
{
    [Theory]
    [InlineData("Asia/Tbilisi")]
    [InlineData("Asia/Yerevan")]
    [InlineData("Europe/Berlin")]
    [InlineData(" UTC ")]
    public void Create_accepts_IANA_zone_ids(string raw)
    {
        TimeZoneId.Create(raw).Value.ShouldBe(raw.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Tbilisi")]
    [InlineData("Georgian Standard Time")]
    [InlineData("GMT+4")]
    public void Create_rejects_unknown_zones(string? raw)
    {
        Should.Throw<DomainException>(() => TimeZoneId.Create(raw)).Code.ShouldBe("tenant.timezone.invalid");
    }

    [Fact]
    public void Tbilisi_converts_to_a_NodaTime_zone_at_UTC_plus_4()
    {
        var zone = TimeZoneId.Tbilisi.ToDateTimeZone();

        zone.GetUtcOffset(NodaTime.Instant.FromUtc(2026, 7, 1, 12, 0)).ShouldBe(NodaTime.Offset.FromHours(4));
    }
}

public sealed class LanguageTests
{
    [Theory]
    [InlineData("ka")]
    [InlineData("RU")]
    [InlineData(" en ")]
    public void Create_accepts_supported_languages(string raw)
    {
        Language.Supported.ShouldContain(Language.Create(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("de")]
    [InlineData("hy")]
    public void Create_rejects_unsupported_languages(string? raw)
    {
        Should.Throw<DomainException>(() => Language.Create(raw)).Code.ShouldBe("tenant.language.unsupported");
    }

    [Fact]
    public void Supported_languages_match_the_web_app_in_order()
    {
        Language.Supported.Select(l => l.Code).ShouldBe(["ka", "ru", "en"]);
    }
}
