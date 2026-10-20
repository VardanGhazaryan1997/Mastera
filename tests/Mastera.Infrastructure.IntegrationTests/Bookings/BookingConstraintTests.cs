using Mastera.Domain.Bookings;
using Mastera.Domain.Catalog;
using Mastera.Domain.Masters;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Npgsql;

namespace Mastera.Infrastructure.IntegrationTests.Bookings;

/// <summary>Task 5.2: the database itself refuses overlapping active bookings and holds for a master.</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class BookingConstraintTests(PostgresFixture db)
{
    private static readonly Instant Now = SystemClock.Instance.GetCurrentInstant();
    private static readonly Instant Ten = Now + Duration.FromDays(10);

    private sealed record Setup(TenantId Tenant, MasterId Luka, MasterId Ana, ServiceId Haircut);

    private async Task<Setup> SetupAsync(CancellationToken ct)
    {
        var tenant = TenantId.New();
        await using var context = db.CreateTestContext(tenant);
        var haircut = Service.Create("Haircut", Duration.FromMinutes(60), 35m);
        var luka = Master.Create("Luka", null, Now);
        var ana = Master.Create("Ana", null, Now);
        context.Services.Add(haircut);
        context.Masters.AddRange(luka, ana);
        await context.SaveChangesAsync(ct);
        return new Setup(tenant, luka.Id, ana.Id, haircut.Id);
    }

    private static Booking At(Setup s, MasterId master, Instant start, int minutes = 60) =>
        Booking.Create(master, s.Haircut, null, "Guest", start, Duration.FromMinutes(minutes), 35m, BookingSource.Manual, true, null, Now);

    private async Task SaveAsync(Setup s, CancellationToken ct, params Booking[] bookings)
    {
        await using var context = db.CreateTestContext(s.Tenant);
        context.Bookings.AddRange(bookings);
        await context.SaveChangesAsync(ct);
    }

    private static void ShouldBeExclusionViolation(DbUpdateException e) =>
        e.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.ExclusionViolation);

    [Fact]
    public async Task Two_overlapping_active_bookings_for_one_master_are_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = await SetupAsync(ct);
        await SaveAsync(s, ct, At(s, s.Luka, Ten));

        var error = await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(s, ct, At(s, s.Luka, Ten + Duration.FromMinutes(30))));

        ShouldBeExclusionViolation(error);
    }

    [Fact]
    public async Task Back_to_back_bookings_are_fine()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = await SetupAsync(ct);

        await SaveAsync(s, ct, At(s, s.Luka, Ten), At(s, s.Luka, Ten + Duration.FromMinutes(60)));
    }

    [Fact]
    public async Task Different_masters_can_be_booked_at_the_same_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = await SetupAsync(ct);

        await SaveAsync(s, ct, At(s, s.Luka, Ten), At(s, s.Ana, Ten));
    }

    [Fact]
    public async Task A_cancelled_booking_frees_its_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = await SetupAsync(ct);
        var first = At(s, s.Luka, Ten);
        await SaveAsync(s, ct, first);

        await using (var context = db.CreateTestContext(s.Tenant))
        {
            var loaded = await context.Bookings.SingleAsync(b => b.Id == first.Id, ct);
            loaded.CancelByClient(Now);
            await context.SaveChangesAsync(ct);
        }

        await SaveAsync(s, ct, At(s, s.Luka, Ten));
    }

    [Fact]
    public async Task Two_overlapping_holds_for_one_master_are_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var s = await SetupAsync(ct);
        await using (var context = db.CreateTestContext(s.Tenant))
        {
            context.SlotHolds.Add(SlotHold.Create(s.Luka, s.Haircut, Ten, Duration.FromMinutes(60), 35m, Now));
            await context.SaveChangesAsync(ct);
        }

        await using var second = db.CreateTestContext(s.Tenant);
        second.SlotHolds.Add(SlotHold.Create(s.Luka, s.Haircut, Ten + Duration.FromMinutes(15), Duration.FromMinutes(60), 35m, Now));

        ShouldBeExclusionViolation(await Should.ThrowAsync<DbUpdateException>(() => second.SaveChangesAsync(ct)));
    }
}
