using Mastera.Api.Auth;
using Mastera.Api.Availability;
using Mastera.Api.Catalog;
using Mastera.Api.Invitations;
using Mastera.Api.Masters;
using Mastera.Api.Schedules;
using Mastera.Api.Salons;
using Mastera.Api.Tenancy;
using Mastera.Application;
using Mastera.Application.Users;
using Mastera.Infrastructure;
using Mastera.Infrastructure.Email;
using System.Text.Json.Serialization;
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
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<AppLinkOptions>(builder.Configuration.GetSection(AppLinkOptions.SectionName));

var app = builder.Build();

// Unhandled exceptions and empty error responses (404, 405, ...) become RFC 9457 ProblemDetails.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

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

app.Run();

#pragma warning disable CA1050 // Program must stay in the global namespace for top-level statements.
/// <summary>Exposed so functional tests can host the API with WebApplicationFactory.</summary>
public partial class Program
{
}
#pragma warning restore CA1050
