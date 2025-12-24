using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using App.Application.Common.Security;
using App.Web.Areas.Shared.Models;

namespace App.Web.Areas.Admin.Pages.Shared.Models;

[Area("Admin")]
[Authorize(Policy = AppClaimTypes.IsAdmin)]
public abstract class BaseAdminPageModel : BasePageModel { }
