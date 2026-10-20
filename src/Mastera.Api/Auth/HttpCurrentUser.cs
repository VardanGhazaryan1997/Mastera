using System.Security.Claims;
using Mastera.Application.Users;
using Mastera.Domain.Memberships;

namespace Mastera.Api.Auth;

/// <summary>Reads the user and their role in the current salon from the request's claims.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Principal?.Identity?.IsAuthenticated == true && Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public MembershipRole? Role =>
        Principal?.HasClaim(c => c.Type == MasteraClaims.TenantId) == true
        && Enum.TryParse<MembershipRole>(Principal.FindFirstValue(ClaimTypes.Role), out var role)
            ? role
            : null;
}
