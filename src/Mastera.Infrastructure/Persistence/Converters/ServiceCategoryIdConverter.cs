using Mastera.Domain.Catalog;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

public sealed class ServiceCategoryIdConverter()
    : ValueConverter<ServiceCategoryId, Guid>(id => id.Value, value => new ServiceCategoryId(value));
