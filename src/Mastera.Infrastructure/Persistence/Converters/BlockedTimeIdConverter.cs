using Mastera.Domain.Bookings;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

public sealed class BlockedTimeIdConverter()
    : ValueConverter<BlockedTimeId, Guid>(id => id.Value, value => new BlockedTimeId(value));
