using Mastera.Domain.Tenants;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

public sealed class TenantIdConverter()
    : ValueConverter<TenantId, Guid>(id => id.Value, value => new TenantId(value));
