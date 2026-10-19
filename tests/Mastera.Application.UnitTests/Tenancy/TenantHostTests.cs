using Mastera.Application.Tenancy;

namespace Mastera.Application.UnitTests.Tenancy;

public sealed class TenantHostTests
{
    [Theory]
    [InlineData("barber-house.mastera.ge", "mastera.ge", "barber-house")]
    [InlineData("Barber-House.Mastera.GE", "mastera.ge", "barber-house")]
    [InlineData("nails24.mastera.ge.", "mastera.ge", "nails24")]
    [InlineData("barber.localhost", "localhost", "barber")]
    [InlineData("a.b.mastera.ge", "mastera.ge", "a.b")]
    public void Subdomain_of_the_root_domain_is_a_salon_candidate(string host, string root, string expected)
    {
        var result = TenantHost.Parse(host, root);

        result.Kind.ShouldBe(TenantHostKind.Subdomain);
        result.Subdomain.ShouldBe(expected);
    }

    [Theory]
    [InlineData("mastera.ge")]
    [InlineData("www.mastera.ge")]
    [InlineData("MASTERA.GE")]
    [InlineData("mastera.ge.")]
    public void Root_domain_and_www_are_the_platform_site(string host)
    {
        var result = TenantHost.Parse(host, "mastera.ge");

        result.Kind.ShouldBe(TenantHostKind.Platform);
        result.Subdomain.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("evilmastera.ge")]
    [InlineData("mastera.ge.evil.com")]
    public void Other_hosts_are_not_salons(string? host)
    {
        TenantHost.Parse(host, "mastera.ge").Kind.ShouldBe(TenantHostKind.Other);
    }

    [Fact]
    public void Root_domain_is_required()
    {
        Should.Throw<ArgumentException>(() => TenantHost.Parse("a.mastera.ge", " "));
    }
}
