using Mastera.Application;
using Mastera.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Unhandled exceptions and empty error responses (404, 405, ...) become RFC 9457 ProblemDetails.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness: the process is up. Readiness checks (database, storage) are added in later epics.
app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposed so functional tests can host the API with WebApplicationFactory.</summary>
public partial class Program
{
}
