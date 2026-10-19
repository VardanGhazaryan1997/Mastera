namespace Mastera.Domain.Memberships;

/// <summary>What a person may do in one salon. The same person can have different roles in different salons.</summary>
public enum MembershipRole
{
    /// <summary>Created the salon; everything, including billing and deleting the salon.</summary>
    Owner,

    /// <summary>Manages masters, services, schedules and bookings.</summary>
    Admin,

    /// <summary>Manages bookings and the calendar for all masters; no settings, no revenue.</summary>
    Receptionist,

    /// <summary>Sees and manages their own calendar and bookings.</summary>
    Master,
}

public static class MembershipRoles
{
    public static bool CanManageSalon(this MembershipRole role) => role is MembershipRole.Owner or MembershipRole.Admin;
}
