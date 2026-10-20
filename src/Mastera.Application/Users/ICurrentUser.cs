using Mastera.Domain.Memberships;

namespace Mastera.Application.Users;

/// <summary>Who is making the request, and their role in the current salon (if any).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    MembershipRole? Role { get; }
}
