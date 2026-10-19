using Mastera.Application.Tenancy;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.IntegrationTests.Tenancy;

/// <summary>Task 1.3: the EF Core query filter and the SaveChanges interceptor (superuser connection, no RLS).</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TenantQueryFilterTests(PostgresFixture db)
{
    [Fact]
    public async Task New_rows_are_stamped_with_the_current_tenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = TenantId.New();
        var note = new Note("hello");

        await using (var context = db.CreateTestContext(tenant))
        {
            context.Notes.Add(note);
            await context.SaveChangesAsync(ct);
        }

        await using var check = db.CreateTestContext(tenant);
        var saved = await check.Notes.SingleAsync(n => n.Id == note.Id, ct);
        saved.TenantId.ShouldBe(tenant);
    }

    [Fact]
    public async Task Queries_return_only_the_current_tenants_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = TenantId.New();
        var b = TenantId.New();
        await db.SeedNotesAsync(a, "a1", "a2");
        await db.SeedNotesAsync(b, "b1");

        await using var context = db.CreateTestContext(a);
        var texts = await context.Notes.Select(n => n.Text).ToListAsync(ct);

        texts.ShouldBe(["a1", "a2"], ignoreOrder: true);
    }

    [Fact]
    public async Task Without_a_tenant_queries_return_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.SeedNotesAsync(TenantId.New(), "someone's note");

        await using var context = db.CreateTestContext(tenant: null);

        (await context.Notes.AnyAsync(ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Saving_without_a_tenant_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = db.CreateTestContext(tenant: null);
        context.Notes.Add(new Note("orphan"));

        await Should.ThrowAsync<TenantIsolationException>(() => context.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Inserting_a_row_for_another_tenant_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = db.CreateTestContext(TenantId.New());
        context.Notes.Add(Note.ForTenant(TenantId.New(), "sneaky"));

        await Should.ThrowAsync<TenantIsolationException>(() => context.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Updating_another_tenants_row_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var ids = await db.SeedNotesAsync(TenantId.New(), "theirs");

        await using var context = db.CreateTestContext(TenantId.New());
        var note = await context.Notes.IgnoreQueryFilters().SingleAsync(n => n.Id == ids[0], ct);
        note.ChangeText("hacked");

        await Should.ThrowAsync<TenantIsolationException>(() => context.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Deleting_another_tenants_row_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var ids = await db.SeedNotesAsync(TenantId.New(), "theirs");

        await using var context = db.CreateTestContext(TenantId.New());
        var note = await context.Notes.IgnoreQueryFilters().SingleAsync(n => n.Id == ids[0], ct);
        context.Notes.Remove(note);

        await Should.ThrowAsync<TenantIsolationException>(() => context.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Updating_your_own_row_works()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = TenantId.New();
        var ids = await db.SeedNotesAsync(tenant, "draft");

        await using (var context = db.CreateTestContext(tenant))
        {
            var note = await context.Notes.SingleAsync(n => n.Id == ids[0], ct);
            note.ChangeText("final");
            await context.SaveChangesAsync(ct);
        }

        await using var check = db.CreateTestContext(tenant);
        (await check.Notes.SingleAsync(n => n.Id == ids[0], ct)).Text.ShouldBe("final");
    }
}
