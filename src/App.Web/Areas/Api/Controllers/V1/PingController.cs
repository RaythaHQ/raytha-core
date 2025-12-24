using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using App.Application.Common.Security;
using App.Application.OrganizationSettings.Queries;
using App.Web.Authentication;

namespace App.Web.Areas.Api.Controllers.V1;

[Authorize(Policy = AppApiAuthorizationHandler.POLICY_PREFIX + AppClaimTypes.IsAdmin)]
public class PingController : BaseController
{
    [HttpGet(Name = "ping")]
    public async Task<ActionResult<HealthCheckResult>> Get()
    {
        var response = await Mediator.Send(new GetOrganizationSettings.Query());
        return new HealthCheckResult
        {
            Success = response.Success,
            Version = CurrentVersion.Version,
            OrganizationName = response.Result.OrganizationName,
        };
    }
}

public class HealthCheckResult
{
    public bool Success { get; set; }
    public string Version { get; set; }
    public string OrganizationName { get; set; }
}
