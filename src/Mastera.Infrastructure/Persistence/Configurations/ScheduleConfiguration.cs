using Mastera.Domain.Masters;
using Mastera.Domain.Schedules;
using Mastera.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mastera.Infrastructure.Persistence.Configurations;

public sealed class MasterScheduleConfiguration : IEntityTypeConfiguration<MasterSchedule>
{
    public void Configure(EntityTypeBuilder<MasterSchedule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MasterSchedules");
        builder.HasKey(s => s.MasterId);
        builder.Property(s => s.Pattern)
            .HasConversion(ScheduleJson.PatternConverter, ScheduleJson.PatternComparer)
            .HasColumnType("jsonb");

        builder.HasOne<Master>()
            .WithOne()
            .HasForeignKey<MasterSchedule>(s => s.MasterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Exceptions)
            .WithOne()
            .HasForeignKey(e => e.MasterId)
            .HasPrincipalKey(s => s.MasterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Exceptions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ScheduleExceptionConfiguration : IEntityTypeConfiguration<ScheduleException>
{
    public void Configure(EntityTypeBuilder<ScheduleException> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ScheduleExceptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Hours)
            .HasConversion(ScheduleJson.DayConverter, ScheduleJson.DayComparer)
            .HasColumnType("jsonb");
        builder.Property(e => e.Note).HasMaxLength(ScheduleException.NoteMaxLength);
        builder.HasIndex(e => new { e.MasterId, e.From });
    }
}
