using Microsoft.AspNetCore.Identity;
using NodaTime;

namespace Mastera.Infrastructure.Identity;

/// <summary>A login account. Global: one person can work in several salons (see Membership).</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public string DisplayName { get; set; } = string.Empty;

    public Instant CreatedAt { get; set; }
}
