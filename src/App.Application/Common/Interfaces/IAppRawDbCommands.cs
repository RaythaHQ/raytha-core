namespace App.Application.Common.Interfaces;

/// <summary>
/// Interface for executing raw database commands that require database-specific SQL.
/// </summary>
public interface IAppRawDbCommands
{
    /// <summary>
    /// Clears all audit logs from the database using TRUNCATE for optimal performance.
    /// </summary>
    Task ClearAuditLogsAsync(CancellationToken cancellationToken = default);
}

