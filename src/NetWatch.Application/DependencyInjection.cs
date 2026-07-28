using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NetWatch.Application.Dashboard;
using NetWatch.Application.Devices;
using NetWatch.Application.Incidents;
using NetWatch.Application.Probes;

namespace NetWatch.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IProbeService, ProbeService>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Picks up every validator in this assembly, so a new request type is validated as
        // soon as its validator exists — no registration list to forget to update.
        services.AddValidatorsFromAssemblyContaining<CreateDeviceRequestValidator>();

        return services;
    }
}
