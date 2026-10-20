using Mastera.Domain.Invitations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mastera.Infrastructure.Persistence.Converters;

public sealed class InvitationIdConverter()
    : ValueConverter<InvitationId, Guid>(id => id.Value, value => new InvitationId(value));
