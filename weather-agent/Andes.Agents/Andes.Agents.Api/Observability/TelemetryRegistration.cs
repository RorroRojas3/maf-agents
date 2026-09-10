using Andes.Agents.Api.Configuration;
using Andes.Agents.Api.Options;
using Andes.Agents.Common.Constants;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;

namespace Andes.Agents.Api.Observability;

internal static class TelemetryRegistration
{
    public static IServiceCollection AddAndesTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(TelemetryOptions.SectionName);
        TelemetryOptions options = section.Get<TelemetryOptions>() ?? new();

        services.AddOptions<TelemetryOptions>().Bind(section);

        OpenTelemetryBuilder telemetry = services.AddOpenTelemetry();

        // The distro brings ASP.NET Core and HttpClient instrumentation with it; adding those packages again breaks it.
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            telemetry.UseAzureMonitor(monitor =>
            {
                monitor.ConnectionString = options.ConnectionString;

                if (options.UseManagedIdentity)
                {
                    monitor.Credential = AzureCredentials.Create(configuration);
                }
            });
        }

        telemetry
            .WithTracing(tracing => tracing.AddSource(TelemetryNames.Source))
            .WithMetrics(metrics => metrics.AddMeter(TelemetryNames.Source));

        return services;
    }
}
