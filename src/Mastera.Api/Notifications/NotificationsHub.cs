using System.Security.Claims;
using Mastera.Api.Auth;
using Mastera.Application.Masters;
using Mastera.Application.Notifications;
using Mastera.Application.Tenancy;
using Mastera.Domain.Masters;
using Mastera.Domain.Memberships;
using Mastera.Domain.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Mastera.Api.Notifications;

/// <summary>
/// Task 6.3: live notifications for the dashboard. On connecting, the front desk joins its salon's desk group
/// and a master joins their own group, so a master only hears about their own bookings.
/// Connect to /hubs/notifications?tenant={slug}.
/// </summary>
[Authorize(Policy = Policies.SalonMember)]
public sealed class NotificationsHub(ITenantLookupById tenants, ITenantSwitcher tenantSwitcher, IMasterRepository masters) : Hub
{
    public const string NotificationMethod = "notification";

    public static string DeskGroup(TenantId tenantId) => $"tenant:{tenantId.Value:N}:desk";

    public static string MasterGroup(TenantId tenantId, MasterId masterId) => $"tenant:{tenantId.Value:N}:master:{masterId.Value:N}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user is null
            || !Guid.TryParse(user.FindFirstValue(MasteraClaims.TenantId), out var tenantGuid)
            || !Enum.TryParse<MembershipRole>(user.FindFirstValue(ClaimTypes.Role), out var role))
        {
            Context.Abort();
            return;
        }

        var tenantId = new TenantId(tenantGuid);
        if (role is MembershipRole.Owner or MembershipRole.Admin or MembershipRole.Receptionist)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DeskGroup(tenantId), Context.ConnectionAborted);
        }

        // Hub methods run in their own scope, so the salon is set again here before looking up the master.
        if (Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && await tenants.FindByIdAsync(tenantId, Context.ConnectionAborted) is { } tenant)
        {
            tenantSwitcher.Use(tenant);
            if (await masters.FindByUserAsync(userId, Context.ConnectionAborted) is { } master)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, MasterGroup(tenantId, master.Id), Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }
}

/// <summary>Sends a notification to the salon's front desk and to the master it is about.</summary>
public sealed class SignalRNotifier(IHubContext<NotificationsHub> hub) : IRealtimeNotifier
{
    public Task PublishAsync(TenantId tenantId, MasterId masterId, LiveNotification notification, CancellationToken cancellationToken) =>
        hub.Clients
            .Groups(NotificationsHub.DeskGroup(tenantId), NotificationsHub.MasterGroup(tenantId, masterId))
            .SendAsync(NotificationsHub.NotificationMethod, notification, cancellationToken);
}
