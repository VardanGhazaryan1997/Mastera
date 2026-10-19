using Mastera.Domain.Common;
using Mastera.Domain.Tenants;

namespace Mastera.Domain.UnitTests.Tenants;

public sealed class TenantSlugTests
{
    [Theory]
    [InlineData("barber-house", "barber-house")]
    [InlineData("  Barber-House  ", "barber-house")]
    [InlineData("NAILS24", "nails24")]
    [InlineData("abc", "abc")]
    public void Create_normalizes_to_trimmed_lowercase(string raw, string expected)
    {
        TenantSlug.Create(raw).Value.ShouldBe(expected);
    }

    [Fact]
    public void Create_accepts_the_maximum_length()
    {
        var raw = new string('a', TenantSlug.MaxLength);

        TenantSlug.Create(raw).Value.Length.ShouldBe(TenantSlug.MaxLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public void Create_rejects_too_short(string? raw)
    {
        Should.Throw<DomainException>(() => TenantSlug.Create(raw)).Code.ShouldBe("tenant.slug.length");
    }

    [Fact]
    public void Create_rejects_too_long()
    {
        var raw = new string('a', TenantSlug.MaxLength + 1);

        Should.Throw<DomainException>(() => TenantSlug.Create(raw)).Code.ShouldBe("tenant.slug.length");
    }

    [Theory]
    [InlineData("-barber")]
    [InlineData("barber-")]
    [InlineData("barber--house")]
    [InlineData("barber house")]
    [InlineData("barber_house")]
    [InlineData("barber.house")]
    [InlineData("ბარბერი")]
    [InlineData("барбер")]
    public void Create_rejects_invalid_characters_and_hyphen_placement(string raw)
    {
        Should.Throw<DomainException>(() => TenantSlug.Create(raw)).Code.ShouldBe("tenant.slug.format");
    }

    [Theory]
    [InlineData("www")]
    [InlineData("API")]
    [InlineData("admin")]
    [InlineData("mastera")]
    public void Create_rejects_reserved_names(string raw)
    {
        Should.Throw<DomainException>(() => TenantSlug.Create(raw)).Code.ShouldBe("tenant.slug.reserved");
    }

    [Fact]
    public void Slugs_with_the_same_value_are_equal()
    {
        TenantSlug.Create("Barber-House").ShouldBe(TenantSlug.Create("barber-house"));
    }
}

public sealed class TenantSlugTryCreateTests
{
    [Fact]
    public void TryCreate_returns_the_slug_for_valid_input()
    {
        TenantSlug.TryCreate("Barber-House", out var slug).ShouldBeTrue();
        slug!.Value.ShouldBe("barber-house");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ab")]
    [InlineData("bad--slug")]
    [InlineData("www")]
    public void TryCreate_returns_false_instead_of_throwing(string? raw)
    {
        TenantSlug.TryCreate(raw, out var slug).ShouldBeFalse();
        slug.ShouldBeNull();
    }
}
