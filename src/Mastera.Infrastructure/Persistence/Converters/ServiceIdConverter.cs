using Mastera.Domain.Catalog;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

public sealed class ServiceIdConverter()
    : ValueConverter<ServiceId, Guid>(id => id.Value, value => new ServiceId(value));
