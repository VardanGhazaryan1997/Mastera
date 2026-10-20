using Mastera.Domain.Bookings;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

public sealed class BookingIdConverter()
    : ValueConverter<BookingId, Guid>(id => id.Value, value => new BookingId(value));
