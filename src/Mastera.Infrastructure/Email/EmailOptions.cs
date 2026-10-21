namespace Mastera.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "localhost";

    public int SmtpPort { get; set; } = 1025;

    public bool EnableSsl { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string From { get; set; } = "Mastera <no-reply@mastera.ge>";
}

public sealed class AppLinkOptions
{
    public const string SectionName = "App";

    /// <summary>Where the dashboard runs, e.g. http://localhost:5173 or https://app.mastera.ge.</summary>
    public Uri DashboardUrl { get; set; } = new("http://localhost:5173");

    /// <summary>The public booking site of a salon; {slug} is replaced, e.g. https://{slug}.mastera.ge.</summary>
    public string BookingSiteUrl { get; set; } = "http://{slug}.localhost:5174";

    /// <summary>Signs the clients' "manage booking" links. Set a long random value in production.</summary>
    public string LinkSecret { get; set; } = "development-only-link-secret-change-me";
}
