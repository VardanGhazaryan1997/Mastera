using Mastera.Domain.Catalog;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using NodaTime;

namespace Mastera.Domain.UnitTests.Masters;

public sealed class MasterTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 20, 9, 0);

    private static readonly Service Haircut = Service.Create("Haircut", Duration.FromMinutes(45), 35m);
    private static readonly Service Beard = Service.Create("Beard trim", Duration.FromMinutes(20), 15m);

    private static Master Luka() => Master.Create("Luka", "Fades and classic cuts", Now);

    [Fact]
    public void Create_makes_an_active_master()
    {
        var master = Luka();

        master.DisplayName.ShouldBe("Luka");
        master.Bio.ShouldBe("Fades and classic cuts");
        master.IsActive.ShouldBeTrue();
        master.UserId.ShouldBeNull();
        master.CreatedAt.ShouldBe(Now);
        master.Services.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("L")]
    public void Create_rejects_a_bad_name(string? name)
    {
        Should.Throw<DomainException>(() => Master.Create(name, null, Now)).Code.ShouldBe("master.name.length");
    }

    [Fact]
    public void Create_rejects_a_too_long_bio()
    {
        Should.Throw<DomainException>(() => Master.Create("Luka", new string('x', 501), Now)).Code.ShouldBe("master.bio.length");
    }

    [Fact]
    public void Deactivate_and_activate()
    {
        var master = Luka();

        master.Deactivate();
        master.IsActive.ShouldBeFalse();
        master.Activate();
        master.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void LinkUser_links_once_and_refuses_another_account()
    {
        var master = Luka();
        var user = Guid.NewGuid();

        master.LinkUser(user);
        master.LinkUser(user);
        master.UserId.ShouldBe(user);

        Should.Throw<DomainException>(() => master.LinkUser(Guid.NewGuid())).Code.ShouldBe("master.user.already_linked");
    }

    [Fact]
    public void Without_overrides_the_master_uses_the_service_price_and_duration()
    {
        var master = Luka();

        master.SetServices([new ServiceOffer(Haircut)]);

        var offer = master.Services.ShouldHaveSingleItem();
        offer.EffectivePrice(Haircut).ShouldBe(35m);
        offer.EffectiveDuration(Haircut).ShouldBe(Duration.FromMinutes(45));
        master.Offers(Haircut.Id).ShouldBeTrue();
        master.Offers(Beard.Id).ShouldBeFalse();
    }

    [Fact]
    public void Overrides_replace_the_service_price_and_duration()
    {
        var master = Luka();

        master.SetServices([new ServiceOffer(Haircut, 50m, Duration.FromMinutes(60))]);

        var offer = master.Services.ShouldHaveSingleItem();
        offer.EffectivePrice(Haircut).ShouldBe(50m);
        offer.EffectiveDuration(Haircut).ShouldBe(Duration.FromMinutes(60));
    }

    [Fact]
    public void SetServices_replaces_the_list()
    {
        var master = Luka();
        master.SetServices([new ServiceOffer(Haircut), new ServiceOffer(Beard)]);

        master.SetServices([new ServiceOffer(Beard, 20m)]);

        var offer = master.Services.ShouldHaveSingleItem();
        offer.ServiceId.ShouldBe(Beard.Id);
        offer.PriceOverride.ShouldBe(20m);
    }

    [Fact]
    public void SetServices_rejects_duplicates()
    {
        Should.Throw<DomainException>(() => Luka().SetServices([new ServiceOffer(Haircut), new ServiceOffer(Haircut)]))
            .Code.ShouldBe("master.service.duplicate");
    }

    [Fact]
    public void SetServices_checks_overrides_with_the_service_rules()
    {
        Should.Throw<DomainException>(() => Luka().SetServices([new ServiceOffer(Haircut, -5m)]))
            .Code.ShouldBe("service.price.invalid");
        Should.Throw<DomainException>(() => Luka().SetServices([new ServiceOffer(Haircut, null, Duration.FromMinutes(7))]))
            .Code.ShouldBe("service.duration.invalid");
    }

    [Fact]
    public void An_archived_service_cannot_be_newly_assigned()
    {
        var archived = Service.Create("Old service", Duration.FromMinutes(30), 10m);
        archived.Archive();

        Should.Throw<DomainException>(() => Luka().SetServices([new ServiceOffer(archived)]))
            .Code.ShouldBe("master.service.archived");
    }

    [Fact]
    public void EffectivePrice_refuses_a_different_service()
    {
        var master = Luka();
        master.SetServices([new ServiceOffer(Haircut)]);

        Should.Throw<ArgumentException>(() => master.Services.Single().EffectivePrice(Beard));
    }
}
