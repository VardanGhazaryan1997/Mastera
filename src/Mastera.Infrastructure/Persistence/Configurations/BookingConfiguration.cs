using Mastera.Domain.Bookings;
using Mastera.Domain.Catalog;
using Mastera.Domain.Clients;
using Mastera.Domain.Masters;
using Mastera.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastera.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Clients");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(Client.NameMaxLength).IsRequired();
        builder.Property(c => c.Phone)
            .HasConversion(phone => phone.Value, value => PhoneNumber.Create(value))
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Notes).HasMaxLength(Client.NotesMaxLength);
        builder.HasIndex(c => new { c.TenantId, c.Phone }).IsUnique();
    }
}

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Bookings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.ClientId).HasConversion<ClientIdConverter>();
        builder.Property(b => b.GuestName).HasMaxLength(Booking.GuestNameMaxLength);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(b => b.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(b => b.Price).HasPrecision(10, 2);
        builder.Property(b => b.FinalPrice).HasPrecision(10, 2);
        builder.Property(b => b.Note).HasMaxLength(Booking.NoteMaxLength);
        builder.HasIndex(b => new { b.MasterId, b.Start });
        builder.HasIndex(b => b.ClientId);

        builder.HasOne<Master>().WithMany().HasForeignKey(b => b.MasterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Service>().WithMany().HasForeignKey(b => b.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Client>().WithMany().HasForeignKey(b => b.ClientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SlotHoldConfiguration : IEntityTypeConfiguration<SlotHold>
{
    public void Configure(EntityTypeBuilder<SlotHold> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SlotHolds");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Price).HasPrecision(10, 2);
        builder.HasIndex(h => new { h.MasterId, h.Start });
        builder.HasOne<Master>().WithMany().HasForeignKey(h => h.MasterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BlockedTimeConfiguration : IEntityTypeConfiguration<BlockedTime>
{
    public void Configure(EntityTypeBuilder<BlockedTime> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlockedTimes");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Reason).HasMaxLength(BlockedTime.ReasonMaxLength);
        builder.HasIndex(b => new { b.MasterId, b.Start });
        builder.HasOne<Master>().WithMany().HasForeignKey(b => b.MasterId).OnDelete(DeleteBehavior.Cascade);
    }
}
