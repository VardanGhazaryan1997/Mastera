using Mastera.Domain.Catalog;
using Mastera.Domain.Common;
using NodaTime;

namespace Mastera.Domain.UnitTests.Catalog;

public sealed class ServiceCategoryTests
{
    [Fact]
    public void Create_trims_the_name()
    {
        var category = ServiceCategory.Create("  Haircuts ", sortOrder: 2);

        category.Name.ShouldBe("Haircuts");
        category.SortOrder.ShouldBe(2);
        category.Id.Value.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("H")]
    public void Create_rejects_a_bad_name(string? name)
    {
        Should.Throw<DomainException>(() => ServiceCategory.Create(name)).Code.ShouldBe("category.name.length");
    }

    [Fact]
    public void Rename_validates_the_name()
    {
        var category = ServiceCategory.Create("Haircuts");

        category.Rename("Nails");
        category.Name.ShouldBe("Nails");
        Should.Throw<DomainException>(() => category.Rename(new string('x', 61)));
    }
}

public sealed class ServiceTests
{
    private static Service Haircut() => Service.Create("Men's haircut", Duration.FromMinutes(45), 35m);

    [Fact]
    public void Create_sets_every_field()
    {
        var category = ServiceCategoryId.New();

        var service = Service.Create(" Men's haircut ", Duration.FromMinutes(45), 35.50m, category, "  With wash ");

        service.Name.ShouldBe("Men's haircut");
        service.Duration.ShouldBe(Duration.FromMinutes(45));
        service.Price.ShouldBe(35.50m);
        service.CategoryId.ShouldBe(category);
        service.Description.ShouldBe("With wash");
        service.IsArchived.ShouldBeFalse();
    }

    [Fact]
    public void An_empty_description_is_stored_as_none()
    {
        Service.Create("Haircut", Duration.FromMinutes(30), 20m, description: "   ").Description.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(481)]
    public void Duration_must_be_5_to_480_minutes_in_steps_of_5(int minutes)
    {
        Should.Throw<DomainException>(() => Service.Create("Haircut", Duration.FromMinutes(minutes), 20m))
            .Code.ShouldBe("service.duration.invalid");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(90)]
    [InlineData(480)]
    public void Valid_durations_are_accepted(int minutes)
    {
        Service.Create("Haircut", Duration.FromMinutes(minutes), 20m).Duration.TotalMinutes.ShouldBe(minutes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000.01)]
    [InlineData(10.005)]
    public void Price_must_be_0_to_100000_with_two_decimals(double price)
    {
        Should.Throw<DomainException>(() => Service.Create("Haircut", Duration.FromMinutes(30), (decimal)price))
            .Code.ShouldBe("service.price.invalid");
    }

    [Fact]
    public void A_free_service_is_allowed()
    {
        Service.Create("Consultation", Duration.FromMinutes(15), 0m).Price.ShouldBe(0m);
    }

    [Fact]
    public void Update_changes_fields_only_when_all_are_valid()
    {
        var service = Haircut();

        Should.Throw<DomainException>(() => service.Update("Beard trim", Duration.FromMinutes(3), 15m));

        service.Name.ShouldBe("Men's haircut");
        service.Update("Beard trim", Duration.FromMinutes(20), 15m);
        service.Name.ShouldBe("Beard trim");
        service.Duration.ShouldBe(Duration.FromMinutes(20));
    }

    [Fact]
    public void Archive_and_restore()
    {
        var service = Haircut();

        service.Archive();
        service.IsArchived.ShouldBeTrue();

        service.Restore();
        service.IsArchived.ShouldBeFalse();
    }
}
