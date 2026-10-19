namespace Mastera.Api.Auth;

public static class MasteraClaims
{
    /// <summary>Added to the user for the request once their membership in the current salon is known.</summary>
    public const string TenantId = "mastera:tenant";

    /// <summary>Request header the dashboard sends to say which of the user's salons it is working in.</summary>
    public const string TenantHeader = "X-Mastera-Tenant";
}

public static class Policies
{
    /// <summary>Signed in and a member of the current salon (any role).</summary>
    public const string SalonMember = "SalonMember";

    /// <summary>Owner or Admin of the current salon.</summary>
    public const string SalonAdmin = "SalonAdmin";
}
