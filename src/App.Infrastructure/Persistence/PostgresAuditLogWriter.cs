using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Domain.Entities;

namespace App.Infrastructure.Persistence;

/// <summary>
/// Writes audit log entries to PostgreSQL.
/// Always registered, always WritesOnly (commands only).
/// This is the source of truth for the in-app Audit Logs UI.
/// Synchronous write (~1-5ms) to ensure consistency before returning to the user.
/// </summary>
public class PostgresAuditLogWriter : IAuditLogWriter
{
    private readonly IAppDbContext _db;

    public PostgresAuditLogWriter(IAppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// PostgreSQL always operates in WritesOnly mode.
    /// Query logging goes only to additional sinks (Loki, OTEL).
    /// </summary>
    public AuditLogMode Mode => AuditLogMode.WritesOnly;

    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        var auditLog = new AuditLog
        {
            Id = entry.Id,
            Category = entry.Category,
            Request = entry.RequestPayload,
            UserEmail = entry.UserEmail ?? string.Empty,
            IpAddress = entry.IpAddress ?? string.Empty,
            EntityId = entry.EntityId,
            CreationTime = entry.Timestamp,
        };

        _db.AuditLogs.Add(auditLog);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

