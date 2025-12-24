using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.ValueObjects;
using CSharpVitamins;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.EmailTemplates.Queries;

public class GetEmailTemplateRevisionsByTemplateId
{
    public record Query
        : GetPagedEntitiesInputDto,
            IRequest<IQueryResponseDto<ListResultDto<EmailTemplateRevisionDto>>>
    {
        public ShortGuid Id { get; init; }
        public override string OrderBy { get; init; } = $"CreationTime {SortOrder.Descending}";
    }

    public class Handler
        : IRequestHandler<Query, IQueryResponseDto<ListResultDto<EmailTemplateRevisionDto>>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<ListResultDto<EmailTemplateRevisionDto>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var query = _db
                .EmailTemplateRevisions.AsNoTracking()
                .AsQueryable()
                .Include(p => p.EmailTemplate)
                .Include(p => p.CreatorUser)
                .Where(p => p.EmailTemplateId == request.Id.Guid);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .ApplyPaginationInput(request)
                .Select(EmailTemplateRevisionDto.GetProjection())
                .ToArrayAsync(cancellationToken);

            return new QueryResponseDto<ListResultDto<EmailTemplateRevisionDto>>(
                new ListResultDto<EmailTemplateRevisionDto>(items, total)
            );
        }
    }
}
