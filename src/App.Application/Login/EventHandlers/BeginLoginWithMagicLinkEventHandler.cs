using CSharpVitamins;
using Mediator;
using Microsoft.EntityFrameworkCore;
using App.Application.Common.Interfaces;
using App.Application.Common.Models.RenderModels;
using App.Domain.Common;
using App.Domain.Entities;
using App.Domain.Events;

namespace App.Application.Login.EventHandlers;

public class BeginLoginWithMagicLinkEventHandler
    : INotificationHandler<BeginLoginWithMagicLinkEvent>
{
    private readonly IEmailer _emailerService;
    private readonly IAppDbContext _db;
    private readonly IRenderEngine _renderEngineService;
    private readonly IRelativeUrlBuilder _relativeUrlBuilderService;
    private readonly ICurrentOrganization _currentOrganization;

    public BeginLoginWithMagicLinkEventHandler(
        ICurrentOrganization currentOrganization,
        IAppDbContext db,
        IEmailer emailerService,
        IRenderEngine renderEngineService,
        IRelativeUrlBuilder relativeUrlBuilderService
    )
    {
        _db = db;
        _emailerService = emailerService;
        _renderEngineService = renderEngineService;
        _relativeUrlBuilderService = relativeUrlBuilderService;
        _currentOrganization = currentOrganization;
    }

    public async ValueTask Handle(
        BeginLoginWithMagicLinkEvent notification,
        CancellationToken cancellationToken
    )
    {
        if (notification.SendEmail)
        {
            var renderTemplate = await _db.EmailTemplates
                .AsNoTracking()
                .FirstAsync(
                    p => p.DeveloperName == BuiltInEmailTemplate.LoginBeginLoginWithMagicLinkEmail,
                    cancellationToken
                );
            SendBeginLoginWithMagicLink_RenderModel entity =
                new SendBeginLoginWithMagicLink_RenderModel
                {
                    Id = (ShortGuid)notification.User.Id,
                    FirstName = notification.User.FirstName,
                    LastName = notification.User.LastName,
                    EmailAddress = notification.User.EmailAddress,
                    LoginWithMagicLinkCompleteUrl = notification.User.IsAdmin
                        ? _relativeUrlBuilderService.AdminLoginWithMagicLinkCompleteUrl(
                            notification.Token,
                            notification.ReturnUrl
                        )
                        : _relativeUrlBuilderService.UserLoginWithMagicLinkCompleteUrl(
                            notification.Token,
                            notification.ReturnUrl
                        ),
                    SsoId = notification.User.SsoId,
                    AuthenticationScheme = notification.User.AuthenticationScheme?.DeveloperName,
                    IsAdmin = notification.User.IsAdmin,
                    MagicLinkExpiresInSeconds = notification.MagicLinkExpiresInSeconds,
                };

            var wrappedModel = new Wrapper_RenderModel
            {
                CurrentOrganization = CurrentOrganization_RenderModel.GetProjection(
                    _currentOrganization
                ),
                Target = entity,
            };

            string subject = _renderEngineService.RenderAsHtml(
                renderTemplate.Subject,
                wrappedModel
            );
            string content = _renderEngineService.RenderAsHtml(
                renderTemplate.Content,
                wrappedModel
            );
            var emailMessage = new EmailMessage
            {
                Content = content,
                To = new List<string> { entity.EmailAddress },
                Subject = subject,
            };
            await _emailerService.SendEmailAsync(emailMessage, cancellationToken);
        }
    }
}
