using Mastera.Domain.Schedules;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

public sealed class ScheduleExceptionIdConverter()
    : ValueConverter<ScheduleExceptionId, Guid>(id => id.Value, value => new ScheduleExceptionId(value));
