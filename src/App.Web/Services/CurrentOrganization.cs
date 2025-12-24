using App.Application.AuthenticationSchemes;
using App.Application.Common.Interfaces;
using App.Application.Common.Utils;
using App.Application.OrganizationSettings;
using App.Domain.ValueObjects;

namespace App.Web.Services;

public class CurrentOrganization : ICurrentOrganization
{
    private readonly IOrganizationSettingsCache _cache;
    private readonly ICurrentOrganizationConfiguration _configuration;

    public CurrentOrganization(
        IOrganizationSettingsCache cache,
        ICurrentOrganizationConfiguration configuration
    )
    {
        _cache = cache;
        _configuration = configuration;
    }

    private OrganizationSettingsDto? OrganizationSettings => _cache.GetOrganizationSettings();

    public IEnumerable<AuthenticationSchemeDto> AuthenticationSchemes =>
        _cache.GetAuthenticationSchemes();

    public bool EmailAndPasswordIsEnabledForAdmins =>
        AuthenticationSchemes.Any(p =>
            p.IsEnabledForAdmins
            && p.AuthenticationSchemeType.DeveloperName == AuthenticationSchemeType.EmailAndPassword
        );
    public bool EmailAndPasswordIsEnabledForUsers =>
        AuthenticationSchemes.Any(p =>
            p.IsEnabledForUsers
            && p.AuthenticationSchemeType.DeveloperName == AuthenticationSchemeType.EmailAndPassword
        );

    public bool InitialSetupComplete => OrganizationSettings != null;

    public string OrganizationName => OrganizationSettings?.OrganizationName;

    public string WebsiteUrl => OrganizationSettings?.WebsiteUrl;

    public string TimeZone => OrganizationSettings?.TimeZone;

    public string SmtpDefaultFromAddress => OrganizationSettings?.SmtpDefaultFromAddress;

    public string SmtpDefaultFromName => OrganizationSettings?.SmtpDefaultFromName;

    public string DateFormat => OrganizationSettings?.DateFormat;

    public OrganizationTimeZoneConverter TimeZoneConverter =>
        OrganizationTimeZoneConverter.From(TimeZone, DateFormat);

    public string PathBase => _configuration.PathBase;
    public string RedirectWebsite => _configuration.RedirectWebsite;
}
