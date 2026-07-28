using System.Text.Json.Serialization;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using NetWatch.Api.Extensions;
using NetWatch.Api.Hubs;
using NetWatch.Api.Middleware;
using NetWatch.Application;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Infrastructure;
using NetWatch.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

EnsureDevelopmentSigningKey(builder);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// SignalR is the transport for live updates; the Application layer only knows the
// IMonitoringNotifier contract, which this binds to the hub.
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
    {
        // SignalR serialises with its own options, entirely separate from MVC's. Without
        // this, controllers would send "Down" while the hub sent 3 for the same enum, and
        // a client applying a live update would poison its own state with a value that
        // does not match anything the REST API ever returns.
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddScoped<IMonitoringNotifier, SignalRMonitoringNotifier>();

builder.Services.AddJwtAuthentication();
builder.Services.AddSpaCors();
builder.Services.AddSwagger();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums travel as names, not integers. A client reading "Down" needs no shared
        // lookup table, and inserting a new enum member cannot silently change the meaning
        // of a value already in the wild.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddFluentValidationAutoValidation();

// Model-binding failures produce the same problem+json shape the exception handler emits,
// so clients only ever parse one error format.
builder.Services.AddProblemDetails();
builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressMapClientErrors = false);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "NetWatch API v1"));
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// The published container serves the built Angular bundle from wwwroot, so in production
// the SPA and the API share an origin and CORS stops mattering.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors(ApiServiceCollectionExtensions.SpaCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");
app.MapHealthChecks("/health");

// Client-side routes fall through to the SPA entry point, so deep links and refreshes
// work instead of returning 404.
app.MapFallbackToFile("index.html");

await InitialiseDatabaseAsync(app);

await app.RunAsync();

/// <summary>
/// Generates an ephemeral JWT signing key when running in Development without one.
///
/// This keeps a working key out of source control while still letting a fresh clone run
/// with no setup. Outside Development nothing is generated, so a deployment missing its
/// key fails at startup (JwtOptions is validated on start) rather than quietly signing
/// tokens with a value an attacker could read in the repository.
///
/// The key changes on every restart, so tokens issued before a restart stop validating.
/// That is the correct trade in development and unacceptable anywhere else.
/// </summary>
static void EnsureDevelopmentSigningKey(WebApplicationBuilder builder)
{
    if (!builder.Environment.IsDevelopment())
    {
        return;
    }

    if (!string.IsNullOrWhiteSpace(builder.Configuration["Jwt:SigningKey"]))
    {
        return;
    }

    var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));

    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Jwt:SigningKey"] = key
    });

    Console.WriteLine("[dev] No Jwt:SigningKey configured — generated an ephemeral one. Tokens will not survive a restart.");
}

/// <summary>
/// Applies migrations and seeds baseline data before the first request is served.
/// </summary>
static async Task InitialiseDatabaseAsync(WebApplication app)
{
    var options = app.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();

    if (!options.MigrateOnStartup && !options.SeedOnStartup)
    {
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var initialiser = scope.ServiceProvider.GetRequiredService<DbInitializer>();

    if (options.MigrateOnStartup)
    {
        await initialiser.MigrateAsync();
    }

    if (options.SeedOnStartup)
    {
        await initialiser.SeedAsync();
    }
}

/// <summary>
/// Exposed so the test project can drive the host through
/// <c>WebApplicationFactory&lt;Program&gt;</c>, which needs a nameable entry-point type.
/// </summary>
public partial class Program;
