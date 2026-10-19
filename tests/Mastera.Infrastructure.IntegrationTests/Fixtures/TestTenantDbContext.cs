using Mastera.Application.Tenancy;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.IntegrationTests.Fixtures;

/// <summary>A tenant-owned entity that exists only in tests, so tenancy can be tested before real ones exist.</summary>
public sealed class Note : ITenantOwned
{
    public Note(string text)
    {
        Id = Guid.NewGuid();
        Text = text;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public string Text { get; private set; }

    public static Note ForTenant(TenantId tenant, string text) => new(text) { TenantId = tenant };

    public void ChangeText(string text) => Text = text;
}

public sealed class TestTenantDbContext : MasteraDbContext
{
    public const string NotesTable = "Notes";

    public TestTenantDbContext(DbContextOptions<TestTenantDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(note =>
        {
            note.ToTable(NotesTable);
            note.HasKey(n => n.Id);
            note.Property(n => n.Id).ValueGeneratedNever();
            note.Property(n => n.Text).HasMaxLength(200);
        });

        // After adding Note, so the base class applies the tenant filter to it.
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class FixedTenantContext(TenantId? tenantId) : ITenantContext
{
    public CurrentTenant? Current { get; } = tenantId is { } id
        ? new CurrentTenant(id, TenantSlug.Create("test-salon"), "Test salon")
        : null;
}
