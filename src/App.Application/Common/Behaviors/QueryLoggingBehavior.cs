using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Mediator;
using Microsoft.Extensions.Logging;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;

namespace App.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that logs query executions to audit log writers configured with Mode = All.
/// PostgreSQL (WritesOnly) never receives query logs - only Loki/OTEL when configured.
/// </summary>
public class QueryLoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IEnumerable<IAuditLogWriter> _auditLogWriters;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<QueryLoggingBehavior<TMessage, TResponse>> _logger;

    public QueryLoggingBehavior(
        IEnumerable<IAuditLogWriter> auditLogWriters,
        ICurrentUser currentUser,
        ILogger<QueryLoggingBehavior<TMessage, TResponse>> logger)
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
        bool isLoggableQuery = interfaces.Any(p => p == typeof(ILoggableQuery));

        // If not a loggable query, just pass through
        if (!isLoggableQuery)
        {
            return await next(message, cancellationToken);
        }

        // Check if any writers accept queries (Mode = All)
        var queryWriters = _auditLogWriters.Where(w => w.Mode == AuditLogMode.All).ToList();
        if (queryWriters.Count == 0)
        {
            // No writers configured for queries, skip logging
            return await next(message, cancellationToken);
        }

        // Track duration
        var stopwatch = Stopwatch.StartNew();
        var response = await next(message, cancellationToken);
        stopwatch.Stop();

        // Determine if we should log the full result
        bool logFullResult = ShouldLogFullResult(message);

        // Check if the response indicates success
        dynamic responseAsDynamic = response as dynamic;
        dynamic messageAsDynamic = message as dynamic;
        bool success = false;
        string? responsePayload = null;

        try
        {
            success = responseAsDynamic.Success;
            
            if (logFullResult && success)
            {
                responsePayload = SerializeResponse(responseAsDynamic);
            }
        }
        catch
        {
            // If we can't determine success, assume true
            success = true;
        }

        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Category = messageAsDynamic.GetLogName(),
            RequestType = "Query",
            RequestPayload = JsonSerializer.Serialize(messageAsDynamic),
            ResponsePayload = responsePayload,
            Success = success,
            DurationMs = stopwatch.ElapsedMilliseconds,
            UserEmail = _currentUser.EmailAddress,
            IpAddress = _currentUser.RemoteIpAddress,
            EntityId = null, // Queries don't have entity IDs
            Timestamp = DateTime.UtcNow,
        };

        // Write only to writers with Mode = All
        await WriteToQueryWritersAsync(queryWriters, entry, cancellationToken);

        return response;
    }

    private bool ShouldLogFullResult(TMessage message)
    {
        var attribute = message.GetType().GetCustomAttribute<LogQueryResultAttribute>();
        return attribute?.LogFullResult ?? false;
    }

    private string? SerializeResponse(dynamic response)
    {
        try
        {
            // Try to serialize the Result property
            var result = response.Result;
            if (result == null) return null;
            
            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                MaxDepth = 10,
                WriteIndented = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to serialize query response for audit log");
            return "[Serialization failed]";
        }
    }

    private async Task WriteToQueryWritersAsync(
        List<IAuditLogWriter> queryWriters,
        AuditLogEntry entry,
        CancellationToken cancellationToken)
    {
        foreach (var writer in queryWriters)
        {
            try
            {
                await writer.WriteAsync(entry, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the request
                _logger.LogWarning(
                    ex,
                    "Failed to write query audit log entry {EntryId} to {WriterType}",
                    entry.Id,
                    writer.GetType().Name);
            }
        }
    }
}

