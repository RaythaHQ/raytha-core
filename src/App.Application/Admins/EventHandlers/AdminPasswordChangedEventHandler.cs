using CSharpVitamins;
using Mediator;
using Microsoft.EntityFrameworkCore;
using App.Application.Common.Interfaces;
using App.Application.Common.Models.RenderModels;
using App.Domain.Common;
using App.Domain.Entities;
using App.Domain.Events;

namespace App.Application.Admins.EventHandlers;

public class AdminPasswordChangedEventHandler : INotificationHandler<AdminPasswordChangedEvent>
{
    private readonly IEmailer _emailerService;
    private readonly IAppDbContext _db;
    private readonly IRenderEngine _renderEngineService;
    private readonly IRelativeUrlBuilder _relativeUrlBuilderService;
    private readonly ICurrentOrganization _currentOrganization;

    public AdminPasswordChangedEventHandler(
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
        AdminPasswordChangedEvent notification,
        CancellationToken cancellationToken
    )
    {
        if (notification.SendEmail)
        {
            var renderTemplate = await _db.EmailTemplates
                .AsNoTracking()
                .FirstAsync(
                    p => p.DeveloperName == BuiltInEmailTemplate.AdminPasswordChangedEmail,
                    cancellationToken
                );
            SendAdminPasswordChanged_RenderModel entity = new SendAdminPasswordChanged_RenderModel
            {
                Id = (ShortGuid)notification.User.Id,
                FirstName = notification.User.FirstName,
                LastName = notification.User.LastName,
                EmailAddress = notification.User.EmailAddress,
                LoginUrl = _relativeUrlBuilderService.AdminLoginUrl(),
                SsoId = notification.User.SsoId,
                AuthenticationScheme = notification.User.AuthenticationScheme.DeveloperName,
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
            _emailerService.SendEmail(emailMessage);
        }
    }
}
