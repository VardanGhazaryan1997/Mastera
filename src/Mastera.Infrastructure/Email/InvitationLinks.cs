using Mastera.Application.Invitations;
using Microsoft.Extensions.Options;

namespace Mastera.Infrastructure.Email;

public sealed class InvitationLinks(IOptions<AppLinkOptions> options) : IInvitationLinks
{
    public Uri AcceptUrl(string token) =>
        new(options.Value.DashboardUrl, $"/invite?token={Uri.EscapeDataString(token)}");
}
