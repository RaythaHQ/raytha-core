using CSharpVitamins;
using Mediator;
using Microsoft.EntityFrameworkCore;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Domain.Entities;

namespace App.Application.EmailTemplates.Commands;

public class RevertEmailTemplate
{
    public record Command : LoggableEntityRequest<CommandResponseDto<ShortGuid>> { }

    public class Handler : IRequestHandler<Command, CommandResponseDto<ShortGuid>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<CommandResponseDto<ShortGuid>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var entity = await _db.EmailTemplateRevisions
                .Include(p => p.EmailTemplate)
                .FirstAsync(p => p.Id == request.Id.Guid, cancellationToken);

            var newRevision = new EmailTemplateRevision
            {
                EmailTemplateId = entity.EmailTemplateId,
                Content = entity.EmailTemplate.Content,
                Subject = entity.EmailTemplate.Subject,
                Cc = entity.EmailTemplate.Cc,
                Bcc = entity.EmailTemplate.Bcc,
            };

            _db.EmailTemplateRevisions.Add(newRevision);
            entity.EmailTemplate.Subject = entity.Subject;
            entity.EmailTemplate.Content = entity.Content;
            entity.EmailTemplate.Bcc = entity.Bcc;
            entity.EmailTemplate.Cc = entity.Cc;

            await _db.SaveChangesAsync(cancellationToken);

            return new CommandResponseDto<ShortGuid>(entity.EmailTemplateId);
        }
    }
}
