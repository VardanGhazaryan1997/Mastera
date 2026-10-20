using Mastera.Domain.Invitations;

namespace Mastera.Application.Invitations;

public interface IInvitationRepository
{
    void Add(Invitation invitation);

    Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
}

/// <summary>Builds the link people click in the invitation email.</summary>
public interface IInvitationLinks
{
    Uri AcceptUrl(string token);
}
