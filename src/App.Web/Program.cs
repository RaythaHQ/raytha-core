//Force build and push
using App.Application.Common.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

namespace App.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        host.Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog(
                (context, services, configuration) =>
                {
                    var observabilityOptions =
                        context
                            .Configuration.GetSection(ObservabilityOptions.SectionName)
                            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

                    // Base configuration
                    configuration
                        .MinimumLevel.Information()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .MinimumLevel.Override(
                            "Microsoft.Hosting.Lifetime",
                            LogEventLevel.Information
                        )
                        .MinimumLevel.Override(
                            "Microsoft.EntityFrameworkCore",
                            LogEventLevel.Warning
                        )
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty(
                            "Application",
                            observabilityOptions.OpenTelemetry.ServiceName
                        )
                        .Enrich.WithProperty(
                            "Environment",
                            context.HostingEnvironment.EnvironmentName
                        );

                    // Console sink (enabled by default)
                    if (observabilityOptions.Logging.EnableConsole)
                    {
                        configuration.WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                        );
                    }

                    // Loki sink for general logging (optional)
                    if (
                        observabilityOptions.Logging.EnableLoki
                        && !string.IsNullOrEmpty(observabilityOptions.Logging.LokiUrl)
                    )
                    {
                        configuration.WriteTo.GrafanaLoki(
                            observabilityOptions.Logging.LokiUrl,
                            restrictedToMinimumLevel: LogEventLevel.Information
                        );
                    }

                    // Log startup configuration
                    Log.Information(
                        "[Observability] Console logging: {Enabled}",
                        observabilityOptions.Logging.EnableConsole
                    );
                    Log.Information(
                        "[Observability] Loki logging: {Enabled} -> {Url}",
                        observabilityOptions.Logging.EnableLoki,
                        observabilityOptions.Logging.LokiUrl ?? "(not configured)"
                    );
                    Log.Information(
                        "[Observability] OpenTelemetry: {Enabled} -> {Endpoint}",
                        observabilityOptions.OpenTelemetry.Enabled,
                        observabilityOptions.OpenTelemetry.OtlpEndpoint ?? "(not configured)"
                    );
                    Log.Information(
                        "[Observability] Sentry: {Enabled}",
                        !string.IsNullOrEmpty(observabilityOptions.Sentry.Dsn)
                    );

                    // Log audit sink configuration
                    var lokiAudit = observabilityOptions.AuditLog.AdditionalSinks.Loki;
                    var otelAudit = observabilityOptions.AuditLog.AdditionalSinks.OpenTelemetry;
                    Log.Information(
                        "[Observability] PostgreSQL audit: enabled (WritesOnly, always)"
                    );
                    Log.Information(
                        "[Observability] Loki audit sink: {Enabled} (Mode: {Mode})",
                        lokiAudit?.Enabled ?? false,
                        lokiAudit?.Mode.ToString() ?? "N/A"
                    );
                    Log.Information(
                        "[Observability] OTEL audit sink: {Enabled} (Mode: {Mode})",
                        otelAudit?.Enabled ?? false,
                        otelAudit?.Mode.ToString() ?? "N/A"
                    );
                }
            )
            .ConfigureWebHostDefaults(webBuilder =>
            {
                // Configure Sentry only if DSN is provided via environment variable or config
                var sentryDsn = Environment.GetEnvironmentVariable("SENTRY_DSN");
                if (!string.IsNullOrEmpty(sentryDsn))
                {
                    webBuilder.UseSentry(options =>
                    {
                        options.Dsn = sentryDsn;
                        options.SendDefaultPii = false; // HIPAA compliance - don't send PII
                        options.AttachStacktrace = true;
                    });
                }

                webBuilder.UseStartup<Startup>();
            });
}
