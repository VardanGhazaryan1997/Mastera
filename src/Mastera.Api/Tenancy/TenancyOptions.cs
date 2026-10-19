namespace Mastera.Api.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    /// <summary>The domain salons are subdomains of: "mastera.ge" in production, "localhost" in development.</summary>
    public string RootDomain { get; set; } = "mastera.ge";
}
