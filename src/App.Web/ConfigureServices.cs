using System.Text.Json;
using CSharpVitamins;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using App.Application.Common.Interfaces;
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
        IWebHostEnvironment environment
    )
    {
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
                RaythaClaimTypes.IsAdmin,
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
                AppApiAuthorizationHandler.POLICY_PREFIX + RaythaClaimTypes.IsAdmin,
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
}
