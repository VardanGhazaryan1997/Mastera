using System.Reflection;
using Mastera.Application.Tenancy;
using Mastera.Domain.Bookings;
using Mastera.Domain.Catalog;
using Mastera.Domain.Clients;
using Mastera.Domain.Invitations;
using Mastera.Domain.Masters;
using Mastera.Domain.Memberships;
using Mastera.Domain.Schedules;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Identity;
using Mastera.Infrastructure.Persistence.Converters;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Persistence;

public class MasteraDbContext : IdentityUserContext<ApplicationUser, Guid>
{
    /// <summary>Used when there is no current tenant. No row ever has this id, so queries return nothing.</summary>
    public static readonly TenantId NoTenant = default;

    private static readonly MethodInfo ConfigureTenantOwnedMethod = typeof(MasteraDbContext)
        .GetMethod(nameof(ConfigureTenantOwned), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ITenantContext _tenantContext;

    public MasteraDbContext(DbContextOptions<MasteraDbContext> options, ITenantContext tenantContext)
        : this((DbContextOptions)options, tenantContext)
    {
    }

    /// <summary>For derived contexts (used by integration tests).</summary>
    protected MasteraDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Master> Masters => Set<Master>();

    public DbSet<MasterSchedule> MasterSchedules => Set<MasterSchedule>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<SlotHold> SlotHolds => Set<SlotHold>();

    public DbSet<BlockedTime> BlockedTimes => Set<BlockedTime>();

    public DbSet<Outbox.OutboxMessage> OutboxMessages => Set<Outbox.OutboxMessage>();

    public DbSet<Outbox.NotificationLogEntry> NotificationLog => Set<Outbox.NotificationLogEntry>();

    public DbSet<Outbox.ScheduledReminder> ScheduledReminders => Set<Outbox.ScheduledReminder>();

    public DbSet<Outbox.TelegramLinkRequest> TelegramLinkRequests => Set<Outbox.TelegramLinkRequest>();

    /// <summary>
    /// The tenant every query is filtered by and every write is stamped with.
    /// Read by EF Core each time a query runs, and by the connection interceptor for row-level security.
    /// </summary>
    public TenantId CurrentTenantId => _tenantContext.Current?.Id ?? NoTenant;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<TenantId>().HaveConversion<TenantIdConverter>();
        configurationBuilder.Properties<MembershipId>().HaveConversion<MembershipIdConverter>();
        configurationBuilder.Properties<InvitationId>().HaveConversion<InvitationIdConverter>();
        configurationBuilder.Properties<ServiceCategoryId>().HaveConversion<ServiceCategoryIdConverter>();
        configurationBuilder.Properties<ServiceId>().HaveConversion<ServiceIdConverter>();
        configurationBuilder.Properties<MasterId>().HaveConversion<MasterIdConverter>();
        configurationBuilder.Properties<ScheduleExceptionId>().HaveConversion<ScheduleExceptionIdConverter>();
        configurationBuilder.Properties<ClientId>().HaveConversion<ClientIdConverter>();
        configurationBuilder.Properties<BookingId>().HaveConversion<BookingIdConverter>();
        configurationBuilder.Properties<SlotHoldId>().HaveConversion<SlotHoldIdConverter>();
        configurationBuilder.Properties<BlockedTimeId>().HaveConversion<BlockedTimeIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Identity tables (users, claims, logins, tokens). Roles are per salon, see Membership.
        base.OnModelCreating(modelBuilder);

        // Needed by the booking overlap constraints (see BookingConstraints).
        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasteraDbContext).Assembly);

        var tenantOwned = modelBuilder.Model.GetEntityTypes()
            .Where(t => t.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        foreach (var clrType in tenantOwned)
        {
            ConfigureTenantOwnedMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ConfigureTenantOwned<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        entity.HasIndex(nameof(ITenantOwned.TenantId));
    }
}
