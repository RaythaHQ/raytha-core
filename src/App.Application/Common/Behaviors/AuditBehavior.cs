using System.Diagnostics;
using System.Text.Json;
using CSharpVitamins;
using Mediator;
using Microsoft.Extensions.Logging;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;

namespace App.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that logs command executions to all registered audit log writers.
/// Only handles commands (LoggableRequest). Queries are handled by QueryLoggingBehavior.
/// </summary>
public class AuditBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IEnumerable<IAuditLogWriter> _auditLogWriters;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuditBehavior<TMessage, TResponse>> _logger;

    public AuditBehavior(
        IEnumerable<IAuditLogWriter> auditLogWriters,
        ICurrentUser currentUser,
        ILogger<AuditBehavior<TMessage, TResponse>> logger)
    {
        _auditLogWriters = auditLogWriters;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken
    )
    {
        var interfaces = message.GetType().GetInterfaces();
        bool isLoggableRequest = interfaces.Any(p => p == typeof(ILoggableRequest));
        bool isLoggableEntityRequest = interfaces.Any(p => p == typeof(ILoggableEntityRequest));

        // If not a loggable command, just pass through
        if (!isLoggableRequest && !isLoggableEntityRequest)
        {
            return await next(message, cancellationToken);
        }

        // Track duration
        var stopwatch = Stopwatch.StartNew();
        var response = await next(message, cancellationToken);
        stopwatch.Stop();

        // Check if the response indicates success
        dynamic responseAsDynamic = response as dynamic;
        dynamic messageAsDynamic = message as dynamic;
        bool success = false;
        
        try
        {
            success = responseAsDynamic.Success;
        }
        catch
        {
            // If we can't determine success, assume true to log
            success = true;
        }

        if (success)
        {
            var entry = new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                Category = messageAsDynamic.GetLogName(),
                RequestType = "Command",
                RequestPayload = JsonSerializer.Serialize(messageAsDynamic),
                ResponsePayload = null, // Commands don't log response payload
                Success = true,
                DurationMs = stopwatch.ElapsedMilliseconds,
                UserEmail = _currentUser.EmailAddress,
                IpAddress = _currentUser.RemoteIpAddress,
                EntityId = isLoggableEntityRequest ? (Guid?)(ShortGuid)messageAsDynamic.Id : null,
                Timestamp = DateTime.UtcNow,
            };

            // Write to all audit log writers
            // Commands go to all writers regardless of their Mode
            await WriteToAllWritersAsync(entry, cancellationToken);
        }

        return response;
    }

    private async Task WriteToAllWritersAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        foreach (var writer in _auditLogWriters)
        {
            try
            {
                await writer.WriteAsync(entry, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the request
                // Each writer should handle its own errors, but we catch here as a safety net
                _logger.LogWarning(
                    ex,
                    "Failed to write audit log entry {EntryId} to {WriterType}",
                    entry.Id,
                    writer.GetType().Name);
            }
        }
    }
}
