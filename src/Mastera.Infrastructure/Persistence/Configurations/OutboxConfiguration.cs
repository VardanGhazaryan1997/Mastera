using Mastera.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastera.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.Error).HasMaxLength(2000);

        // The processor looks for unprocessed messages, oldest first.
        builder.HasIndex(m => m.OccurredAt).HasFilter("\"ProcessedAt\" IS NULL");
    }
}

public sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLogEntry>
{
    public void Configure(EntityTypeBuilder<NotificationLogEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("NotificationLog");
        builder.HasKey(e => e.Key);
        builder.Property(e => e.Key).HasMaxLength(200);
    }
}

public sealed class ScheduledReminderConfiguration : IEntityTypeConfiguration<ScheduledReminder>
{
    public void Configure(EntityTypeBuilder<ScheduledReminder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ScheduledReminders");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Kind).HasMaxLength(32).IsRequired();
        builder.HasIndex(r => r.DueAt).HasFilter("\"SentAt\" IS NULL");
        builder.HasIndex(r => r.BookingId);
    }
}

public sealed class TelegramLinkRequestConfiguration : IEntityTypeConfiguration<TelegramLinkRequest>
{
    public void Configure(EntityTypeBuilder<TelegramLinkRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TelegramLinkRequests");
        builder.HasKey(r => r.CodeHash);
        builder.Property(r => r.CodeHash).HasMaxLength(64);
    }
}
