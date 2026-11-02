using ClyvoCare.Application.Diagnostics;
using ClyvoCare.Infrastructure.Diagnostics;
using OpenTelemetry.Trace;

namespace ClyvoCare.API.Extensions;

public static class ClyvoCareTracingExtensions
{
    public static IServiceCollection AddClyvoCareTracing(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenTelemetry().WithTracing(tracing =>
        {
            tracing.AddSource(ApplicationTelemetry.Source.Name, PersistenceTelemetry.Source.Name)
                .AddAspNetCoreInstrumentation(options =>
                    options.EnrichWithHttpRequest = (activity, _) => activity.SetTag("url.query", null))
                .AddHttpClientInstrumentation(options =>
                    options.EnrichWithHttpRequestMessage = (activity, _) => activity.SetTag("url.full", null));

            if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
                tracing.AddOtlpExporter();
        });
        return services;
    }
}
