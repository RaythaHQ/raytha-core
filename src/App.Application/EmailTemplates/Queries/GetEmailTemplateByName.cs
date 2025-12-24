using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.EmailTemplates.Queries;

public class GetEmailTemplateByName
{
    public record Query : IRequest<IQueryResponseDto<EmailTemplateDto>>
    {
        public string DeveloperName { get; init; }
    }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<EmailTemplateDto>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<EmailTemplateDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var entity = await _db.EmailTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.DeveloperName == request.DeveloperName.ToDeveloperName(),
                    cancellationToken
                );

            if (entity == null)
                throw new NotFoundException("EmailTemplate", request.DeveloperName);

            return new QueryResponseDto<EmailTemplateDto>(EmailTemplateDto.GetProjection(entity));
        }
    }
}
