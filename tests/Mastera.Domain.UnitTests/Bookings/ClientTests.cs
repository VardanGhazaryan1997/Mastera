using Mastera.Domain.Clients;
using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Domain.UnitTests.Bookings;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("+995 555 12 34 56", "+995555123456")]
    [InlineData("555 12-34-56", "+995555123456")]
    [InlineData("(555) 123 456", "+995555123456")]
    [InlineData("995555123456", "+995555123456")]
    [InlineData("00995555123456", "+995555123456")]
    [InlineData("+374 91 234567", "+37491234567")]
    [InlineData("+1 (212) 555-0100", "+12125550100")]
    public void Common_ways_of_typing_a_number_are_normalized(string raw, string expected)
    {
        PhoneNumber.Create(raw).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("+0995555123456")]
    [InlineData("+99555512345678901")]
    [InlineData("555-12-34-5x")]
    [InlineData("032 2 12 34 56")]
    public void Invalid_numbers_are_rejected(string? raw)
    {
        Should.Throw<DomainException>(() => PhoneNumber.Create(raw)).Code.ShouldBe("client.phone.invalid");
    }

    [Fact]
    public void The_same_number_typed_differently_is_equal()
    {
        PhoneNumber.Create("555 12 34 56").ShouldBe(PhoneNumber.Create("+995555123456"));
    }
}

public sealed class ClientTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 21, 9, 0);

    [Fact]
    public void Create_keeps_name_and_phone()
    {
        var client = Client.Create(" Nino ", PhoneNumber.Create("555123456"), Now);

        client.Name.ShouldBe("Nino");
        client.Phone.Value.ShouldBe("+995555123456");
        client.NoShowCount.ShouldBe(0);
    }

    [Fact]
    public void Create_rejects_a_missing_name()
    {
        Should.Throw<DomainException>(() => Client.Create(" ", PhoneNumber.Create("555123456"), Now)).Code.ShouldBe("client.name.length");
    }

    [Fact]
    public void No_shows_are_counted()
    {
        var client = Client.Create("Nino", PhoneNumber.Create("555123456"), Now);

        client.RecordNoShow();
        client.RecordNoShow();

        client.NoShowCount.ShouldBe(2);
    }
}
