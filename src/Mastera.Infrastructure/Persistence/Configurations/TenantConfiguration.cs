using Mastera.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastera.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasMaxLength(Tenant.NameMaxLength)
            .IsRequired();

        builder.Property(t => t.Slug)
            .HasConversion(slug => slug.Value, value => TenantSlug.Create(value))
            .HasMaxLength(TenantSlug.MaxLength)
            .IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.TimeZone)
            .HasConversion(zone => zone.Value, value => TimeZoneId.Create(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.Currency)
            .HasConversion(currency => currency.Code, value => Currency.Create(value))
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(t => t.DefaultLanguage)
            .HasConversion(language => language.Code, value => Language.Create(value))
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(t => t.CreatedAt).IsRequired();
    }
}
