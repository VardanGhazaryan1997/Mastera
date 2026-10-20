using Mastera.Domain.Clients;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

public sealed class ClientIdConverter()
    : ValueConverter<ClientId, Guid>(id => id.Value, value => new ClientId(value));
