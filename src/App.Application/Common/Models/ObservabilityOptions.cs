namespace App.Application.Common.Models;

/// <summary>
/// Root configuration options for observability features.
/// All integrations are opt-in and gracefully degrade when not configured.
/// </summary>
public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public LoggingOptions Logging { get; set; } = new();
    public OpenTelemetryOptions OpenTelemetry { get; set; } = new();
    public SentryOptions Sentry { get; set; } = new();
    public AuditLogOptions AuditLog { get; set; } = new();
}

/// <summary>
/// Configuration for general application logging sinks.
/// </summary>
public class LoggingOptions
{
    public bool EnableConsole { get; set; } = true;
    public bool EnableOpenTelemetry { get; set; } = false;
    public bool EnableLoki { get; set; } = false;
    public string? LokiUrl { get; set; }
}

/// <summary>
/// Configuration for OpenTelemetry tracing, metrics, and logs.
/// </summary>
public class OpenTelemetryOptions
{
    public bool Enabled { get; set; } = false;
    public string? OtlpEndpoint { get; set; }
    public string ServiceName { get; set; } = "app";
}

/// <summary>
/// Configuration for Sentry error tracking.
/// </summary>
public class SentryOptions
{
    public string? Dsn { get; set; }
    public string Environment { get; set; } = "production";
}

/// <summary>
/// Configuration for audit logging.
/// PostgreSQL is always enabled and always WritesOnly - it's the source for the in-app UI.
/// Additional sinks can be configured with their own modes.
/// </summary>
public class AuditLogOptions
{
    /// <summary>
    /// Additional audit log sinks beyond PostgreSQL.
    /// These support configurable modes (WritesOnly or All).
    /// </summary>
    public AdditionalAuditSinkOptions AdditionalSinks { get; set; } = new();
}

/// <summary>
/// Configuration for additional audit log sinks beyond PostgreSQL.
/// </summary>
public class AdditionalAuditSinkOptions
{
    public LokiAuditSinkOptions? Loki { get; set; }
    public OtelAuditSinkOptions? OpenTelemetry { get; set; }
}

/// <summary>
/// Configuration for Loki as an additional audit log sink.
/// </summary>
public class LokiAuditSinkOptions
{
    public bool Enabled { get; set; } = false;
    public string? Url { get; set; }
    public AuditLogMode Mode { get; set; } = AuditLogMode.All;
}

/// <summary>
/// Configuration for OpenTelemetry as an additional audit log sink.
/// </summary>
public class OtelAuditSinkOptions
{
    public bool Enabled { get; set; } = false;
    public AuditLogMode Mode { get; set; } = AuditLogMode.All;
}

/// <summary>
/// Controls what operations are logged to audit sinks.
/// </summary>
public enum AuditLogMode
{
    /// <summary>
    /// Only log commands (mutations/writes).
    /// </summary>
    WritesOnly,

    /// <summary>
    /// Log both commands and queries (reads + writes).
    /// Default for additional sinks to support HIPAA compliance.
    /// </summary>
    All
}

