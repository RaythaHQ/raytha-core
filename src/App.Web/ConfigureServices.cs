using System.Text.Json;
using CSharpVitamins;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Security;
using App.Domain.Entities;
using App.Infrastructure.Persistence;
using App.Web.Authentication;
using App.Web.Middlewares;
using App.Web.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ConfigureServices
{
    public static IServiceCollection AddWebUIServices(
        this IServiceCollection services,
        IWebHostEnvironment environment,
        IConfiguration configuration
    )
    {
        // Bind observability options
        services.Configure<ObservabilityOptions>(
            configuration.GetSection(ObservabilityOptions.SectionName));

        // Configure observability services
        services.AddObservabilityServices(configuration);
        // In development, use less restrictive cookie settings to allow non-HTTPS access
        // from non-localhost hosts (e.g., http://machinename:8888). SameSite=None requires
        // Secure, and Secure cookies are rejected from non-secure origins except localhost.
        var cookieSecurePolicy = environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        var cookieSameSite = environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None;

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(
                CookieAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    options.LoginPath = new PathString("/admin/login-redirect");
                    options.Cookie.IsEssential = true;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = cookieSecurePolicy;
                    options.Cookie.SameSite = cookieSameSite;
                    options.AccessDeniedPath = new PathString("/admin/403");
                    options.ExpireTimeSpan = TimeSpan.FromDays(30);
                    options.EventsType = typeof(CustomCookieAuthenticationEvents);
                }
            );

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AppClaimTypes.IsAdmin,
                policy => policy.Requirements.Add(new IsAdminRequirement())
            );
            options.AddPolicy(
                BuiltInSystemPermission.MANAGE_USERS_PERMISSION,
                policy => policy.Requirements.Add(new ManageUsersRequirement())
            );
            options.AddPolicy(
                BuiltInSystemPermission.MANAGE_ADMINISTRATORS_PERMISSION,
                policy => policy.Requirements.Add(new ManageAdministratorsRequirement())
            );
            options.AddPolicy(
                BuiltInSystemPermission.MANAGE_TEMPLATES_PERMISSION,
                policy => policy.Requirements.Add(new ManageTemplatesRequirement())
            );
            options.AddPolicy(
                BuiltInSystemPermission.MANAGE_AUDIT_LOGS_PERMISSION,
                policy => policy.Requirements.Add(new ManageAuditLogsRequirement())
            );
            options.AddPolicy(
                BuiltInSystemPermission.MANAGE_SYSTEM_SETTINGS_PERMISSION,
                policy => policy.Requirements.Add(new ManageSystemSettingsRequirement())
            );

            options.AddPolicy(
                AppApiAuthorizationHandler.POLICY_PREFIX + AppClaimTypes.IsAdmin,
                policy => policy.Requirements.Add(new ApiIsAdminRequirement())
            );
            options.AddPolicy(
                AppApiAuthorizationHandler.POLICY_PREFIX
                    + BuiltInSystemPermission.MANAGE_SYSTEM_SETTINGS_PERMISSION,
                policy => policy.Requirements.Add(new ApiManageSystemSettingsRequirement())
            );
            options.AddPolicy(
                AppApiAuthorizationHandler.POLICY_PREFIX
                    + BuiltInSystemPermission.MANAGE_USERS_PERMISSION,
                policy => policy.Requirements.Add(new ApiManageUsersRequirement())
            );
            options.AddPolicy(
                AppApiAuthorizationHandler.POLICY_PREFIX
                    + BuiltInSystemPermission.MANAGE_TEMPLATES_PERMISSION,
                policy => policy.Requirements.Add(new ApiManageTemplatesRequirement())
            );
        });

        services.AddScoped<CustomCookieAuthenticationEvents>();
        services
            .AddControllersWithViews(options => { })
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                o.JsonSerializerOptions.WriteIndented = true;
                o.JsonSerializerOptions.Converters.Add(new ShortGuidConverter());
                o.JsonSerializerOptions.Converters.Add(new AuditableUserDtoConverter());
            });
        services.AddMemoryCache();
        services.AddSingleton<IOrganizationSettingsCache, OrganizationSettingsCache>();

        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentOrganization, CurrentOrganization>();
        services.AddScoped<IRelativeUrlBuilder, RelativeUrlBuilder>();
        services.AddScoped<IRenderEngine, RenderEngine>();
        services.AddSingleton<IFileStorageProviderSettings, FileStorageProviderSettings>();
        services.AddSingleton<ICurrentVersion, CurrentVersion>();

        services.AddScoped<IAuthorizationHandler, AppAdminAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, AppApiAuthorizationHandler>();
        services.AddSingleton<
            IAuthorizationMiddlewareResultHandler,
            ApiKeyAuthorizationMiddleware
        >();

        services.AddScoped<ICsvService, CsvService>();

        services.AddRouting();
        services
            .AddDataProtection()
            .SetApplicationName("App")
            .PersistKeysToDbContext<AppDbContext>();
        services.AddHttpContextAccessor();

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<ApiKeySecuritySchemeTransformer>();
        });
        services.AddRazorPages();
        return services;
    }

    /// <summary>
    /// Configures OpenTelemetry and Sentry observability services.
    /// All integrations are optional and gracefully degrade when not configured.
    /// </summary>
    private static IServiceCollection AddObservabilityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        // Configure OpenTelemetry if enabled
        if (options.OpenTelemetry.Enabled && !string.IsNullOrEmpty(options.OpenTelemetry.OtlpEndpoint))
        {
            var serviceName = options.OpenTelemetry.ServiceName;
            var otlpEndpoint = new Uri(options.OpenTelemetry.OtlpEndpoint);

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName)
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "production"
                    }))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = otlpEndpoint;
                    }))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = otlpEndpoint;
                    }));

            // Add OpenTelemetry logging if enabled
            if (options.Logging.EnableOpenTelemetry)
            {
                services.AddLogging(logging =>
                {
                    logging.AddOpenTelemetry(otelLogging =>
                    {
                        otelLogging.SetResourceBuilder(ResourceBuilder.CreateDefault()
                            .AddService(serviceName));
                        otelLogging.AddOtlpExporter(opts =>
                        {
                            opts.Endpoint = otlpEndpoint;
                        });
                    });
                });
            }
        }

        // Note: Sentry is configured via UseSentry() in the Startup Configure method
        // or via environment variables (SENTRY_DSN). The Sentry.AspNetCore middleware
        // will automatically pick up the configuration from environment variables.
        // If you need programmatic configuration, use webBuilder.UseSentry() in Program.cs

        return services;
    }
}
