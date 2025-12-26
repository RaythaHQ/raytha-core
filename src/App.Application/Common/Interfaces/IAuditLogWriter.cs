using App.Application.Common.Models;

namespace App.Application.Common.Interfaces;

/// <summary>
/// Interface for writing audit log entries to various destinations.
/// Implementations may be synchronous (PostgreSQL) or non-blocking (Loki, OTEL).
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>
    /// The mode this writer operates in. Used by behaviors to determine
    /// whether to send query (read) entries to this writer.
    /// </summary>
    AuditLogMode Mode { get; }

    /// <summary>
    /// Writes an audit log entry to this sink.
    /// Implementations should handle their own error recovery and not throw exceptions
    /// that would impact the request pipeline.
    /// </summary>
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken);
}

/// <summary>
/// Represents a single audit log entry that can be written to multiple sinks.
/// </summary>
public record AuditLogEntry
{
    /// <summary>
    /// Unique identifier for this audit log entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Category of the request, typically the command/query name.
    /// Example: "Users.Commands.CreateUser"
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Type of request: "Command" or "Query".
    /// </summary>
    public string RequestType { get; init; } = string.Empty;

    /// <summary>
    /// JSON-serialized request payload.
    /// </summary>
    public string RequestPayload { get; init; } = string.Empty;

    /// <summary>
    /// Optional JSON-serialized response payload.
    /// Only populated for queries when LogFullResult is enabled.
    /// </summary>
    public string? ResponsePayload { get; init; }

    /// <summary>
    /// Whether the request completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Duration of the request in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Email address of the user who made the request, if authenticated.
    /// </summary>
    public string? UserEmail { get; init; }

    /// <summary>
    /// IP address of the client making the request.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// Optional entity ID if this is a LoggableEntityRequest.
    /// </summary>
    public Guid? EntityId { get; init; }

    /// <summary>
    /// UTC timestamp when the request was made.
    /// </summary>
    public DateTime Timestamp { get; init; }
}

