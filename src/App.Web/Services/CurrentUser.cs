using System;
using System.Linq;
using System.Security.Claims;
using CSharpVitamins;
using Microsoft.AspNetCore.Http;
using App.Application.Common.Interfaces;
using App.Application.Common.Security;

namespace App.Web.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor != null
        && _httpContextAccessor.HttpContext != null
        && _httpContextAccessor.HttpContext.User != null
        && _httpContextAccessor.HttpContext.User.Identity.IsAuthenticated;

    public ShortGuid? UserId =>
        IsAuthenticated
            ? _httpContextAccessor
                ?.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                ?.Value
            : null;
    public string FirstName =>
        IsAuthenticated
            ? _httpContextAccessor
                ?.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)
                ?.Value
            : string.Empty;
    public string LastName =>
        IsAuthenticated
            ? _httpContextAccessor
                ?.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Surname)
                ?.Value
            : string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string EmailAddress =>
        IsAuthenticated
            ? _httpContextAccessor
                ?.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)
                ?.Value
            : string.Empty;
    public string SsoId =>
        _httpContextAccessor
            ?.HttpContext.User.Claims.FirstOrDefault(c => c.Type == AppClaimTypes.SsoId)
            ?.Value;
    public string AuthenticationScheme =>
        _httpContextAccessor
            ?.HttpContext.User.Claims.FirstOrDefault(c =>
                c.Type == AppClaimTypes.AuthenticationScheme
            )
            ?.Value;
    public string RemoteIpAddress
    {
        get
        {
            var context = _httpContextAccessor?.HttpContext;
            if (context == null) return null;

            // Check Cloudflare header first
            var cfConnectingIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(cfConnectingIp))
                return cfConnectingIp;

            // Check X-Forwarded-For (set by most reverse proxies)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // X-Forwarded-For can contain multiple IPs, the first is the original client
                return forwardedFor.Split(',')[0].Trim();
            }

            // Fall back to connection IP
            return context.Connection.RemoteIpAddress?.MapToIPv4().ToString();
        }
    }
    public bool IsAdmin =>
        _httpContextAccessor
            ?.HttpContext.User.Claims.FirstOrDefault(c => c.Type == AppClaimTypes.IsAdmin)
            ?.Value != null
            ? Convert.ToBoolean(
                _httpContextAccessor
                    ?.HttpContext.User.Claims.FirstOrDefault(c =>
                        c.Type == AppClaimTypes.IsAdmin
                    )
                    ?.Value
            )
            : false;

    public DateTime? LastModificationTime
    {
        get
        {
            if (IsAuthenticated)
            {
                var lastModified = _httpContextAccessor
                    ?.HttpContext.User.Claims.FirstOrDefault(c =>
                        c.Type == AppClaimTypes.LastModificationTime
                    )
                    ?.Value;
                if (!string.IsNullOrEmpty(lastModified))
                    return Convert.ToDateTime(lastModified);
            }
            return null;
        }
    }

    public string[] Roles =>
        _httpContextAccessor
            ?.HttpContext.User.Claims.Where(c => c.Type == ClaimTypes.Role)
            .Select(p => p.Value)
            .ToArray();
    public string[] UserGroups =>
        _httpContextAccessor
            ?.HttpContext.User.Claims.Where(c => c.Type == AppClaimTypes.UserGroups)
            .Select(p => p.Value)
            .ToArray();
    public string[] SystemPermissions =>
        _httpContextAccessor
            ?.HttpContext.User.Claims.Where(c => c.Type == AppClaimTypes.SystemPermissions)
            .Select(p => p.Value)
            .ToArray();
}
