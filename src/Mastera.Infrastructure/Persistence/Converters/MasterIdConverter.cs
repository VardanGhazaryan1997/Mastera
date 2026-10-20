using Mastera.Domain.Masters;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

public sealed class MasterIdConverter()
    : ValueConverter<MasterId, Guid>(id => id.Value, value => new MasterId(value));
