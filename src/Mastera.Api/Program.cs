using Mastera.Api.Auth;
using Mastera.Api.Availability;
using Mastera.Api.Bookings;
using Mastera.Api.Common;
using Mastera.Api.Catalog;
using Mastera.Api.Invitations;
using Mastera.Api.Masters;
using Mastera.Api.Notifications;
using Mastera.Api.Settings;
using Mastera.Api.Schedules;
using Mastera.Api.Salons;
using Mastera.Api.Tenancy;
using Mastera.Application;
using Mastera.Application.Users;
using Mastera.Infrastructure;
using Mastera.Infrastructure.Email;
using Mastera.Infrastructure.Notifications;
using Mastera.Application.Notifications;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddTenancy(builder.Configuration);
builder.Services.AddMasteraAuth(builder.Environment);
builder.Services.AddHttpContextAccessor();

// Public booking endpoints are anonymous, so limit how often one address can call them (spam, bots).
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(BookingEndpoints.PublicRateLimit, context =>
    {
        var limit = context.RequestServices.GetRequiredService<IConfiguration>().GetValue("RateLimits:PublicBookingPerMinute", 20);
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
    options.OnRejected = (context, _) => new ValueTask(
        ProblemResults.Problem(StatusCodes.Status429TooManyRequests, "Too many requests. Try again in a minute.", "rate_limited")
            .ExecuteAsync(context.HttpContext));
});
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<AppLinkOptions>(builder.Configuration.GetSection(AppLinkOptions.SectionName));

// Epic 6: notifications. SMS and Telegram are only logged until a gateway / bot token is configured.
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.Configure<NotificationWorkerOptions>(builder.Configuration.GetSection(NotificationWorkerOptions.SectionName));
builder.Services.AddHttpClient<ISmsSender, HttpSmsSender>();
builder.Services.AddHttpClient<ITelegramSender, TelegramBotSender>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();
builder.Services.AddHostedService<NotificationWorker>();

var app = builder.Build();

// Unhandled exceptions and empty error responses (404, 405, ...) become RFC 9457 ProblemDetails.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness: the process is up. Readiness checks (database, storage) are added in later epics.
app.MapHealthChecks("/health");
app.MapTenantEndpoints();
app.MapAuthEndpoints();
app.MapSalonEndpoints();
app.MapCatalogEndpoints();
app.MapMasterEndpoints();
app.MapInvitationEndpoints();
app.MapScheduleEndpoints();
app.MapAvailabilityEndpoints();
app.MapBookingEndpoints();
app.MapSettingsEndpoints();
app.MapNotificationEndpoints();

app.Run();

#pragma warning disable CA1050 // Program must stay in the global namespace for top-level statements.
/// <summary>Exposed so functional tests can host the API with WebApplicationFactory.</summary>
public partial class Program
{
}
#pragma warning restore CA1050
