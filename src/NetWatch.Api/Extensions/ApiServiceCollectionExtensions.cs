using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NetWatch.Infrastructure.Identity;

namespace NetWatch.Api.Extensions;

/// <summary>
/// API-layer composition. Like the Infrastructure equivalent, nothing here reads a
/// configuration value while registering: every binding is deferred to the service
/// provider so that sources added after this point — an integration test's overrides,
/// a secret store — are honoured rather than silently missed.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    public const string SpaCorsPolicy = "spa";

    /// <summary>
    /// JWT bearer authentication.
    ///
    /// Every validation switch is set explicitly rather than left to defaults, so the
    /// security posture is readable at a glance. ClockSkew is cut from the default five
    /// minutes: with 15-minute access tokens, five minutes of grace is a third of the
    /// lifetime.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // Browsers cannot set an Authorization header on a WebSocket handshake, so
                // SignalR passes the token as a query parameter. Accepted for hub paths
                // only — allowing it everywhere would leak tokens into server access logs.
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// CORS for the Angular client.
    ///
    /// Origins come from configuration, never a wildcard: <c>AllowCredentials</c> and
    /// <c>AllowAnyOrigin</c> are mutually exclusive, and SignalR needs credentials.
    /// </summary>
    public static IServiceCollection AddSpaCors(this IServiceCollection services)
    {
        services.AddCors();

        services
            .AddOptions<CorsOptions>()
            .Configure<IConfiguration>((cors, configuration) =>
            {
                var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                              ?? ["http://localhost:4200"];

                cors.AddPolicy(SpaCorsPolicy, policy => policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
            });

        return services;
    }

    /// <summary>
    /// Swagger with a bearer-token input, so the whole API is explorable from the browser
    /// without a separate REST client.
    /// </summary>
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "NetWatch API",
                Version = "v1",
                Description = "Network and service availability monitoring: devices, probes, live health and incidents."
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token returned by /api/auth/login.",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
        });

        return services;
    }
}
