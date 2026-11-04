using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(options => options.IncludeFormattedMessage = true);
        // Existing business logs can contain recipient data. Export infrastructure logs only.
        builder.Logging.AddFilter<OpenTelemetryLoggerProvider>((category, level) =>
            level >= LogLevel.Information
            && category is ("Microsoft.Hosting.Lifetime" or "JobExecutor"
                or "Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService"));

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health")
                        && !context.Request.Path.StartsWithSegments("/alive"))
                .AddHttpClientInstrumentation()
                .AddProcessor(new UrlPrivacyProcessor()));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks("/health");
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("live")
            });
        }

        return app;
    }

    private sealed class UrlPrivacyProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            // Acceptance links and OAuth callbacks can contain secrets in paths or queries.
            foreach (var tag in new[] { "url.full", "url.path", "url.query", "http.url", "http.target" })
            {
                activity.SetTag(tag, null);
            }
        }
    }
}
