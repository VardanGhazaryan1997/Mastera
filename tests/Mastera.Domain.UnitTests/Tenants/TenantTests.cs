using Mastera.Domain.Common;
using Mastera.Domain.Tenants;
using NodaTime;

namespace Mastera.Domain.UnitTests.Tenants;

public sealed class TenantTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 19, 12, 0);

    private static Tenant NewTenant(string name = "Barber House") =>
        Tenant.Create(name, TenantSlug.Create("barber-house"), TimeZoneId.Tbilisi, Currency.Gel, Language.Georgian, Now);

    [Fact]
    public void Create_sets_every_property()
    {
        var tenant = NewTenant();

        tenant.Id.Value.ShouldNotBe(Guid.Empty);
        tenant.Name.ShouldBe("Barber House");
        tenant.Slug.Value.ShouldBe("barber-house");
        tenant.TimeZone.ShouldBe(TimeZoneId.Tbilisi);
        tenant.Currency.ShouldBe(Currency.Gel);
        tenant.DefaultLanguage.ShouldBe(Language.Georgian);
        tenant.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Create_gives_each_tenant_a_new_id()
    {
        NewTenant().Id.ShouldNotBe(NewTenant().Id);
    }

    [Fact]
    public void Create_trims_the_name()
    {
        NewTenant("  Nail Studio  ").Name.ShouldBe("Nail Studio");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public void Create_rejects_a_too_short_name(string? name)
    {
        Should.Throw<DomainException>(() => NewTenant(name!)).Code.ShouldBe("tenant.name.length");
    }

    [Fact]
    public void Create_rejects_a_too_long_name()
    {
        var name = new string('x', Tenant.NameMaxLength + 1);

        Should.Throw<DomainException>(() => NewTenant(name)).Code.ShouldBe("tenant.name.length");
    }

    [Fact]
    public void Rename_changes_the_name_and_validates_it()
    {
        var tenant = NewTenant();

        tenant.Rename(" Barber House Vake ");
        tenant.Name.ShouldBe("Barber House Vake");

        Should.Throw<DomainException>(() => tenant.Rename("")).Code.ShouldBe("tenant.name.length");
        tenant.Name.ShouldBe("Barber House Vake");
    }

    [Fact]
    public void ChangeRegionalSettings_updates_zone_currency_and_language()
    {
        var tenant = NewTenant();

        tenant.ChangeRegionalSettings(TimeZoneId.Create("Asia/Yerevan"), Currency.Create("AMD"), Language.Russian);

        tenant.TimeZone.Value.ShouldBe("Asia/Yerevan");
        tenant.Currency.Code.ShouldBe("AMD");
        tenant.DefaultLanguage.ShouldBe(Language.Russian);
    }

    [Fact]
    public void Tenants_are_equal_by_id_only()
    {
        var a = NewTenant();
        var b = NewTenant();

        a.ShouldNotBe(b);
        a.ShouldBe(a);
        (a == b).ShouldBeFalse();
    }
}
