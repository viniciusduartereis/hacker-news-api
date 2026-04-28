using HackerNewsApi.Observability;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HackerNewsApi.Configurations;

public static class TelemetryConfiguration
{
    private const string ModeOtlp = "otlp";
    private const string ModeConsole = "console";

    public static IServiceCollection AddAppInsightsTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = ResolveOtlpEndpoint(configuration);
        var telemetryMode = ResolveTelemetryMode(configuration, otlpEndpoint);

        if (telemetryMode is null)
        {
            return services;
        }

        var serviceName = ResolveServiceName(configuration);
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName);

        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .SetSampler(new AlwaysOnSampler())
                .AddAspNetCoreInstrumentation()
                .AddSource(HackerNewsTelemetry.ActivitySourceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation());

        if (string.Equals(telemetryMode, ModeOtlp, StringComparison.OrdinalIgnoreCase))
        {
            openTelemetry
                .WithTracing(tracing => tracing.AddOtlpExporter(options => ConfigureOtlpExporter(options, otlpEndpoint)))
                .WithMetrics(metrics => metrics.AddOtlpExporter(options => ConfigureOtlpExporter(options, otlpEndpoint)));
        }

        if (string.Equals(telemetryMode, ModeConsole, StringComparison.OrdinalIgnoreCase))
        {
            openTelemetry
                .WithTracing(tracing => tracing.AddConsoleExporter())
                .WithMetrics(metrics => metrics.AddConsoleExporter());
        }

        return services;
    }

    public static ILoggingBuilder AddLoggingInsightsTelemetry(
        this ILoggingBuilder logging,
        IConfiguration configuration)
    {
        var otlpEndpoint = ResolveOtlpEndpoint(configuration);
        var telemetryMode = ResolveTelemetryMode(configuration, otlpEndpoint);

        if (telemetryMode is null)
        {
            return logging;
        }

        var serviceName = ResolveServiceName(configuration);
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName);

        logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;

            if (string.Equals(telemetryMode, ModeOtlp, StringComparison.OrdinalIgnoreCase))
            {
                options.AddOtlpExporter(exporterOptions => ConfigureOtlpExporter(exporterOptions, otlpEndpoint));
            }

            if (string.Equals(telemetryMode, ModeConsole, StringComparison.OrdinalIgnoreCase))
            {
                options.AddConsoleExporter();
            }
        });

        return logging;
    }

    private static string ResolveServiceName(IConfiguration configuration)
    {
        return configuration["OTEL_SERVICE_NAME"]
            ?? configuration["OpenTelemetry:ServiceName"]
            ?? "hacker-news-api";
    }

    private static string? ResolveOtlpEndpoint(IConfiguration configuration)
    {
        return configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? configuration["OpenTelemetry:Otlp:Endpoint"]
            ?? configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"]
            ?? configuration["ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL"];
    }

    private static string? ResolveTelemetryMode(
        IConfiguration configuration,
        string? otlpEndpoint)
    {
        var explicitMode = configuration["Telemetry:Exporter"]
            ?? configuration["TELEMETRY_EXPORTER"];
        if (!string.IsNullOrWhiteSpace(explicitMode))
        {
            return explicitMode.Trim().ToLowerInvariant() switch
            {
                ModeOtlp when !string.IsNullOrWhiteSpace(otlpEndpoint) => ModeOtlp,
                ModeConsole => ModeConsole,
                _ => null
            };
        }

        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? Environments.Production;

        var isDevelopment = string.Equals(
            environmentName,
            Environments.Development,
            StringComparison.OrdinalIgnoreCase);

        if (isDevelopment && !string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            return ModeOtlp;
        }

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            return ModeOtlp;
        }

        return null;
    }

    private static void ConfigureOtlpExporter(OtlpExporterOptions options, string? endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            options.Endpoint = endpointUri;
        }
    }
}
