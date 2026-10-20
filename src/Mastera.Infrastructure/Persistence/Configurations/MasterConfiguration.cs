using Mastera.Domain.Catalog;
using Mastera.Domain.Masters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastera.Infrastructure.Persistence.Configurations;

public sealed class MasterConfiguration : IEntityTypeConfiguration<Master>
{
    public void Configure(EntityTypeBuilder<Master> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Masters");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.DisplayName).HasMaxLength(Master.DisplayNameMaxLength).IsRequired();
        builder.Property(m => m.Bio).HasMaxLength(Master.BioMaxLength);
        builder.HasIndex(m => m.UserId);

        builder.HasMany(m => m.Services)
            .WithOne()
            .HasForeignKey(s => s.MasterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(m => m.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class MasterServiceConfiguration : IEntityTypeConfiguration<MasterService>
{
    public void Configure(EntityTypeBuilder<MasterService> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MasterServices");
        builder.HasKey(s => new { s.MasterId, s.ServiceId });
        builder.Property(s => s.PriceOverride).HasPrecision(10, 2);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(s => s.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
