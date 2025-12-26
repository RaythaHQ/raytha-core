using System.Threading.Channels;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Logging;

/// <summary>
/// Writes audit log entries to Loki/Victoria Logs via Serilog.
/// Non-blocking: uses a bounded channel with a background consumer.
/// WriteAsync returns immediately after queuing (no request latency impact).
/// 
/// This writer uses the application's configured Serilog logger, which should
/// have the Loki sink configured in Program.cs when EnableLoki is true.
/// </summary>
public class LokiAuditLogWriter : IAuditLogWriter, IDisposable
{
    private readonly Channel<AuditLogEntry> _channel;
    private readonly ILogger<LokiAuditLogWriter> _logger;
    private readonly CancellationTokenSource _cts;
    private readonly Task _processingTask;

    public AuditLogMode Mode { get; }

    public LokiAuditLogWriter(
        IOptions<ObservabilityOptions> options,
        ILogger<LokiAuditLogWriter> logger)
    {
        _logger = logger;
        var lokiOptions = options.Value.AuditLog.AdditionalSinks.Loki;
        Mode = lokiOptions?.Mode ?? AuditLogMode.All;

        // Bounded channel prevents memory blowup if Loki is unavailable
        _channel = Channel.CreateBounded<AuditLogEntry>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _cts = new CancellationTokenSource();
        _processingTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
    }

    public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        // Non-blocking write to channel - returns immediately
        if (!_channel.Writer.TryWrite(entry))
        {
            _logger.LogWarning("Loki audit log channel full, dropping oldest entry");
        }
        return Task.CompletedTask;
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var entry in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    WriteToLoki(entry);
                }
                catch (Exception ex)
                {
                    // Log failure but don't crash - audit is best-effort for external sinks
                    _logger.LogWarning(ex, "Failed to write audit log entry {EntryId} to Loki", entry.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LokiAuditLogWriter processing loop failed unexpectedly");
        }
    }

    private void WriteToLoki(AuditLogEntry entry)
    {
        // Use structured logging with properties optimized for Loki/Victoria Logs filtering.
        // The Serilog Loki sink will pick these up and send them with proper labels.
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["AuditLogId"] = entry.Id,
            ["Category"] = entry.Category,
            ["RequestType"] = entry.RequestType,
            ["UserEmail"] = entry.UserEmail ?? "anonymous",
            ["IpAddress"] = entry.IpAddress ?? "unknown",
            ["EntityId"] = entry.EntityId?.ToString() ?? "none",
            ["Success"] = entry.Success,
            ["DurationMs"] = entry.DurationMs,
            ["AuditTimestamp"] = entry.Timestamp
        }))
        {
            if (entry.Success)
            {
                _logger.LogInformation(
                    "[AUDIT] {RequestType} {Category} by {UserEmail} succeeded in {DurationMs}ms. Request: {RequestPayload}",
                    entry.RequestType,
                    entry.Category,
                    entry.UserEmail ?? "anonymous",
                    entry.DurationMs,
                    entry.RequestPayload);
            }
            else
            {
                _logger.LogWarning(
                    "[AUDIT] {RequestType} {Category} by {UserEmail} failed in {DurationMs}ms. Request: {RequestPayload}",
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
                    "[AUDIT] Response for {AuditLogId}: {ResponsePayload}",
                    entry.Id,
                    entry.ResponsePayload);
            }
        }
    }

    public void Dispose()
    {
        _channel.Writer.Complete();
        _cts.Cancel();
        
        try
        {
            // Wait briefly for remaining items to be processed
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Ignore cancellation exceptions
        }
        
        _cts.Dispose();
    }
}
