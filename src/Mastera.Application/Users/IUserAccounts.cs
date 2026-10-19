using Mastera.Application.Common;

namespace Mastera.Application.Users;

/// <summary>Login accounts. Implemented with ASP.NET Core Identity in Infrastructure.</summary>
public interface IUserAccounts
{
    /// <summary>Creates an account and returns its user id, or a validation/conflict error.</summary>
    Task<Result<Guid>> CreateAsync(string email, string password, string displayName, CancellationToken cancellationToken);
}
