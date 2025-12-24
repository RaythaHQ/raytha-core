using App.Application.AuthenticationSchemes;
using App.Application.OrganizationSettings;

namespace App.Application.Common.Interfaces;

/// <summary>
/// Provides cached access to organization settings and authentication schemes
/// to avoid database hits on every request.
/// </summary>
public interface IOrganizationSettingsCache
{
    /// <summary>
    /// Gets the cached organization settings, or loads from database if not cached.
    /// </summary>
    OrganizationSettingsDto? GetOrganizationSettings();

    /// <summary>
    /// Gets the cached authentication schemes, or loads from database if not cached.
    /// </summary>
    IEnumerable<AuthenticationSchemeDto> GetAuthenticationSchemes();

    /// <summary>
    /// Invalidates the cached organization settings, forcing a reload on next access.
    /// </summary>
    void InvalidateOrganizationSettings();

    /// <summary>
    /// Invalidates the cached authentication schemes, forcing a reload on next access.
    /// </summary>
    void InvalidateAuthenticationSchemes();

    /// <summary>
    /// Invalidates all cached data.
    /// </summary>
    void InvalidateAll();
}

