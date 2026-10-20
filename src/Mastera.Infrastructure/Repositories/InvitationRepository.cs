using Mastera.Application.Invitations;
using Mastera.Domain.Invitations;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Repositories;

public sealed class InvitationRepository(MasteraDbContext db) : IInvitationRepository
{
    public void Add(Invitation invitation) => db.Invitations.Add(invitation);

    public Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Invitations.SingleOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);
}
