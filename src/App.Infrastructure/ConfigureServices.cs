using System;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Infrastructure.BackgroundTasks;
using App.Infrastructure.Configurations;
using App.Infrastructure.FileStorage;
using App.Infrastructure.Logging;
using App.Infrastructure.Persistence;
using App.Infrastructure.Persistence.Interceptors;
using App.Infrastructure.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ConfigureServices
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var dbConnectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        // PostgreSQL only
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(
                dbConnectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5);
                    npgsqlOptions.MigrationsAssembly("App.Infrastructure");
                }
            );
        });
        services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(dbConnectionString));
        services.AddScoped<IAppDbContext>(provider =>
            provider.GetRequiredService<AppDbContext>()
        );

        // Health checks for PostgreSQL
        services
            .AddHealthChecks()
            .AddNpgSql(dbConnectionString, name: "postgres", timeout: TimeSpan.FromSeconds(5));

        services.AddSingleton<
            ICurrentOrganizationConfiguration,
            CurrentOrganizationConfiguration
        >();
        services.AddSingleton<ISecurityConfiguration, SecurityConfiguration>();
        services.AddScoped<IEmailerConfiguration, EmailerConfiguration>();

        services.AddScoped<IEmailer, Emailer>();
        services.AddTransient<IBackgroundTaskDb, BackgroundTaskDb>();
        services.AddTransient<IAppRawDbInfo, AppRawDbInfo>();
        services.AddTransient<IAppRawDbCommands, AppRawDbCommands>();

        //file storage provider
        var fileStorageProvider = configuration[FileStorageUtility.CONFIG_NAME]
            .IfNullOrEmpty(FileStorageUtility.LOCAL)
            .ToLower();
        if (fileStorageProvider == FileStorageUtility.LOCAL)
        {
            services.AddScoped<IFileStorageProvider, LocalFileStorageProvider>();
        }
        else if (fileStorageProvider.ToLower() == FileStorageUtility.AZUREBLOB)
        {
            services.AddScoped<IFileStorageProvider, AzureBlobFileStorageProvider>();
        }
        else if (fileStorageProvider.ToLower() == FileStorageUtility.S3)
        {
            services.AddScoped<IFileStorageProvider, S3FileStorageProvider>();
        }
        else
        {
            throw new NotImplementedException(
                $"Unsupported file storage provider: {fileStorageProvider}"
            );
        }

        for (int i = 0; i < Convert.ToInt32(configuration["NUM_BACKGROUND_WORKERS"] ?? "4"); i++)
        {
            services.AddSingleton<IHostedService, QueuedHostedService>();
        }
        services.AddScoped<IBackgroundTaskQueue, BackgroundTaskQueue>();

        // Register audit log writers
        services.AddAuditLogWriters(configuration);

        return services;
    }

    /// <summary>
    /// Registers audit log writers based on configuration.
    /// PostgreSQL is always registered. Loki and OTEL are optional.
    /// </summary>
    private static IServiceCollection AddAuditLogWriters(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        // PostgreSQL writer is always registered (source of truth for UI)
        services.AddScoped<IAuditLogWriter, PostgresAuditLogWriter>();

        // Loki writer (optional, non-blocking)
        var lokiOptions = options.AuditLog.AdditionalSinks.Loki;
        if (lokiOptions?.Enabled == true && !string.IsNullOrEmpty(lokiOptions.Url))
        {
            services.AddSingleton<IAuditLogWriter, LokiAuditLogWriter>();
        }

        // OpenTelemetry writer (optional, non-blocking)
        var otelOptions = options.AuditLog.AdditionalSinks.OpenTelemetry;
        if (otelOptions?.Enabled == true)
        {
            services.AddScoped<IAuditLogWriter, OtelAuditLogWriter>();
        }

        return services;
    }
}
