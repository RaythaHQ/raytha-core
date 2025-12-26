using Mediator;

namespace App.Application.Common.Models;

/// <summary>
/// Marker interface for queries that should be logged to audit sinks
/// configured with Mode = All.
/// </summary>
public interface ILoggableQuery { }

/// <summary>
/// Base record for queries that should be logged to audit sinks.
/// Only logged to sinks with Mode = All (not PostgreSQL, which is always WritesOnly).
/// </summary>
/// <typeparam name="T">The response type of the query.</typeparam>
public abstract record LoggableQuery<T> : IRequest<T>, ILoggableQuery
{
    /// <summary>
    /// Gets the log name for this query, used as the Category in audit logs.
    /// </summary>
    public virtual string GetLogName()
    {
        return this.GetType()
            .FullName?.Replace("App.Application.", string.Empty)
            .Replace("+Query", string.Empty) ?? GetType().Name;
    }
}

/// <summary>
/// Attribute to control whether full query results are logged.
/// By default, only metadata (count, success, duration) is logged for performance.
/// Apply this attribute with LogFullResult = true to log the complete response payload.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class LogQueryResultAttribute : Attribute
{
    /// <summary>
    /// When true, the full query result will be serialized and logged.
    /// Default is false (metadata only) for performance and to avoid logging large payloads.
    /// </summary>
    public bool LogFullResult { get; set; } = false;
}

