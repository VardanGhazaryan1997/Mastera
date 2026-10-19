using Mastera.Domain.Tenants;
using Mastera.Infrastructure.IntegrationTests.Fixtures;
using Mastera.Infrastructure.Tenancy;
using NodaTime;

namespace Mastera.Infrastructure.IntegrationTests.Tenancy;

/// <summary>Task 1.2: finding a salon by its address.</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TenantLookupTests(PostgresFixture db)
{
    [Fact]
    public async Task Finds_a_salon_by_its_slug()
    {
        var ct = TestContext.Current.CancellationToken;
        var slug = TenantSlug.Create($"salon-{Guid.NewGuid():N}"[..20]);
        var tenant = Tenant.Create("Nail Studio", slug, TimeZoneId.Tbilisi, Currency.Gel, Language.Georgian, SystemClock.Instance.GetCurrentInstant());
        await using (var context = db.CreateDbContext())
        {
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync(ct);
        }

        await using var lookupContext = db.CreateDbContext();
        var found = await new TenantLookup(lookupContext).FindBySlugAsync(slug, ct);

        found.ShouldNotBeNull();
        found.Id.ShouldBe(tenant.Id);
        found.Slug.ShouldBe(slug);
        found.Name.ShouldBe("Nail Studio");
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_slug()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = db.CreateDbContext();

        var found = await new TenantLookup(context).FindBySlugAsync(TenantSlug.Create("no-such-salon"), ct);

        found.ShouldBeNull();
    }

    [Fact]
    public async Task Slugs_are_unique()
    {
        var ct = TestContext.Current.CancellationToken;
        var slug = TenantSlug.Create($"dup-{Guid.NewGuid():N}"[..20]);
        var now = SystemClock.Instance.GetCurrentInstant();

        await using (var context = db.CreateDbContext())
        {
            context.Tenants.Add(Tenant.Create("First", slug, TimeZoneId.Tbilisi, Currency.Gel, Language.Georgian, now));
            await context.SaveChangesAsync(ct);
        }

        await using var second = db.CreateDbContext();
        second.Tenants.Add(Tenant.Create("Second", slug, TimeZoneId.Tbilisi, Currency.Gel, Language.Georgian, now));

        await Should.ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => second.SaveChangesAsync(ct));
    }
}
