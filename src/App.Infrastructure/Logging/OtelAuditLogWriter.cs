using System.Diagnostics;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Logging;

/// <summary>
/// Writes audit log entries as OpenTelemetry log records.
/// Non-blocking: leverages OTEL SDK's internal batching and async export.
/// Includes trace context for correlation with distributed traces.
/// </summary>
public class OtelAuditLogWriter : IAuditLogWriter
{
    private readonly ILogger<OtelAuditLogWriter> _logger;

    public AuditLogMode Mode { get; }

    public OtelAuditLogWriter(
        IOptions<ObservabilityOptions> options,
        ILogger<OtelAuditLogWriter> logger)
    {
        _logger = logger;
        var otelOptions = options.Value.AuditLog.AdditionalSinks.OpenTelemetry;
        Mode = otelOptions?.Mode ?? AuditLogMode.All;
    }

    public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        // The OTEL SDK automatically batches and exports logs asynchronously.
        // We use structured logging which gets picked up by the OTEL log exporter.
        // Activity.Current provides automatic trace context correlation.

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["audit.id"] = entry.Id,
            ["audit.category"] = entry.Category,
            ["audit.request_type"] = entry.RequestType,
            ["audit.user_email"] = entry.UserEmail,
            ["audit.ip_address"] = entry.IpAddress,
            ["audit.entity_id"] = entry.EntityId,
            ["audit.success"] = entry.Success,
            ["audit.duration_ms"] = entry.DurationMs,
            ["audit.timestamp"] = entry.Timestamp,
            // Include trace context if available
            ["trace.id"] = Activity.Current?.TraceId.ToString(),
            ["span.id"] = Activity.Current?.SpanId.ToString()
        }))
        {
            if (entry.Success)
            {
                _logger.LogInformation(
                    "Audit: {RequestType} {Category} by {UserEmail} succeeded in {DurationMs}ms. Request: {RequestPayload}",
                    entry.RequestType,
                    entry.Category,
                    entry.UserEmail ?? "anonymous",
                    entry.DurationMs,
                    entry.RequestPayload);
            }
            else
            {
                _logger.LogWarning(
                    "Audit: {RequestType} {Category} by {UserEmail} failed in {DurationMs}ms. Request: {RequestPayload}",
                    entry.RequestType,
                    entry.Category,
                    entry.UserEmail ?? "anonymous",
                    entry.DurationMs,
                    entry.RequestPayload);
            }

            // Log response payload separately if present (can be large)
            if (!string.IsNullOrEmpty(entry.ResponsePayload))
            {
                _logger.LogDebug(
                    "Audit Response for {AuditId}: {ResponsePayload}",
                    entry.Id,
                    entry.ResponsePayload);
            }
        }

        return Task.CompletedTask;
    }
}

