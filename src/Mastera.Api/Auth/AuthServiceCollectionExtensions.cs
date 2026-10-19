using Mastera.Domain.Memberships;
using Mastera.Infrastructure.Identity;
using Mastera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Mastera.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    public const string CookieName = "mastera.auth";

    /// <summary>
    /// Backend-for-frontend auth: the React apps never see a token. Identity issues an HttpOnly, SameSite=Strict
    /// cookie; API calls answer 401/403 instead of redirecting to a login page.
    /// </summary>
    public static IServiceCollection AddMasteraAuth(this IServiceCollection services, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<MasteraDbContext>()
            .AddSignInManager();

        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                },
                OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                },
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.SalonMember, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(MasteraClaims.TenantId))
            .AddPolicy(Policies.SalonAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(MasteraClaims.TenantId)
                .RequireRole(nameof(MembershipRole.Owner), nameof(MembershipRole.Admin)));

        return services;
    }
}
