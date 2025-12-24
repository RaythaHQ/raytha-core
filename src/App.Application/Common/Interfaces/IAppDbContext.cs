using Microsoft.EntityFrameworkCore;
using App.Domain.Entities;

namespace App.Application.Common.Interfaces;

public interface IAppDbContext
{
    public DbSet<User> Users { get; }
    public DbSet<Role> Roles { get; }
    public DbSet<UserGroup> UserGroups { get; }
    public DbSet<VerificationCode> VerificationCodes { get; }
    public DbSet<Domain.Entities.OrganizationSettings> OrganizationSettings { get; }
    public DbSet<EmailTemplate> EmailTemplates { get; }
    public DbSet<EmailTemplateRevision> EmailTemplateRevisions { get; }
    public DbSet<AuthenticationScheme> AuthenticationSchemes { get; }
    public DbSet<JwtLogin> JwtLogins { get; }
    public DbSet<OneTimePassword> OneTimePasswords { get; }
    public DbSet<AuditLog> AuditLogs { get; }
    public DbSet<ApiKey> ApiKeys { get; }
    public DbSet<BackgroundTask> BackgroundTasks { get; }
    public DbSet<FailedLoginAttempt> FailedLoginAttempts { get; }
    public DbSet<MediaItem> MediaItems { get; }
    public DbContext DbContext { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

