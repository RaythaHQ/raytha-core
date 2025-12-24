using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using App.Application.AuthenticationSchemes;
using App.Application.Common.Interfaces;
using App.Application.OrganizationSettings;

namespace App.Web.Services;

/// <summary>
/// Thread-safe cache for organization settings and authentication schemes.
/// Uses IMemoryCache to avoid database queries on every request.
/// </summary>
public class OrganizationSettingsCache : IOrganizationSettingsCache
{
    private const string OrgSettingsCacheKey = "OrganizationSettings";
    private const string AuthSchemesCacheKey = "AuthenticationSchemes";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly IServiceProvider _serviceProvider;

    public OrganizationSettingsCache(IMemoryCache cache, IServiceProvider serviceProvider)
    {
        _cache = cache;
        _serviceProvider = serviceProvider;
    }

    public OrganizationSettingsDto? GetOrganizationSettings()
    {
        return _cache.GetOrCreate(OrgSettingsCacheKey, entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            return LoadOrganizationSettings();
        });
    }

    public IEnumerable<AuthenticationSchemeDto> GetAuthenticationSchemes()
    {
        return _cache.GetOrCreate(AuthSchemesCacheKey, entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            return LoadAuthenticationSchemes();
        }) ?? Enumerable.Empty<AuthenticationSchemeDto>();
    }

    public void InvalidateOrganizationSettings()
    {
        _cache.Remove(OrgSettingsCacheKey);
    }

    public void InvalidateAuthenticationSchemes()
    {
        _cache.Remove(AuthSchemesCacheKey);
    }

    public void InvalidateAll()
    {
        InvalidateOrganizationSettings();
        InvalidateAuthenticationSchemes();
    }

    private OrganizationSettingsDto? LoadOrganizationSettings()
    {
        // Create a scope to get IAppDbContext (scoped service)
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var entity = db.OrganizationSettings
            .AsNoTracking()
            .FirstOrDefault();

        return OrganizationSettingsDto.GetProjection(entity);
    }

    private IEnumerable<AuthenticationSchemeDto> LoadAuthenticationSchemes()
    {
        // Create a scope to get IAppDbContext (scoped service)
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var entities = db.AuthenticationSchemes
            .AsNoTracking()
            .Include(p => p.LastModifierUser)
            .ToList();

        return entities.Select(AuthenticationSchemeDto.GetProjection).ToList();
    }
}

