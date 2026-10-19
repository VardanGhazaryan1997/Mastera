using Mastera.Domain.Memberships;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

public sealed class MembershipIdConverter()
    : ValueConverter<MembershipId, Guid>(id => id.Value, value => new MembershipId(value));
