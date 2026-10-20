using Mastera.Application.Common;
using Mastera.Application.Users;
using Microsoft.AspNetCore.Identity;
using NodaTime;

namespace Mastera.Infrastructure.Identity;

public sealed class UserAccounts(UserManager<ApplicationUser> userManager, IClock clock) : IUserAccounts
{
    public async Task<Result<Guid>> CreateAsync(string email, string password, string displayName, CancellationToken cancellationToken)
    {
        var trimmedEmail = (email ?? string.Empty).Trim();
        var user = new ApplicationUser
        {
            UserName = trimmedEmail,
            Email = trimmedEmail,
            DisplayName = (displayName ?? string.Empty).Trim(),
            CreatedAt = clock.GetCurrentInstant(),
        };

        var result = await userManager.CreateAsync(user, password ?? string.Empty);
        if (result.Succeeded)
        {
            return Result.Success(user.Id);
        }

        var codes = result.Errors.Select(e => e.Code).ToList();
        var details = string.Join(" ", result.Errors.Select(e => e.Description));

        if (codes.Any(c => c is "DuplicateEmail" or "DuplicateUserName"))
        {
            return Result.Failure<Guid>(Error.Conflict("auth.email.taken", "An account with this email already exists."));
        }

        if (codes.Any(c => c is "InvalidEmail" or "InvalidUserName"))
        {
            return Result.Failure<Guid>(Error.Validation("auth.email.invalid", "Enter a valid email address."));
        }

        if (codes.Any(c => c.StartsWith("Password", StringComparison.Ordinal)))
        {
            return Result.Failure<Guid>(Error.Validation("auth.password.weak", details));
        }

        return Result.Failure<Guid>(Error.Validation("auth.user.invalid", details));
    }

    public async Task<Guid?> FindIdByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync((email ?? string.Empty).Trim());
        return user?.Id;
    }
}
