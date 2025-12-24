using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.EmailTemplates.Queries;

public class GetEmailTemplates
{
    public record Query
        : GetPagedEntitiesInputDto,
            IRequest<IQueryResponseDto<ListResultDto<EmailTemplateDto>>>
    {
        public override string OrderBy { get; init; } = $"Subject {SortOrder.ASCENDING}";
    }

    public class Handler
        : IRequestHandler<Query, IQueryResponseDto<ListResultDto<EmailTemplateDto>>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<ListResultDto<EmailTemplateDto>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var query = _db
                .EmailTemplates.AsNoTracking()
                .Include(p => p.LastModifierUser)
                .AsQueryable();

            if (!string.IsNullOrEmpty(request.Search))
            {
                var searchQuery = request.Search.ToLower();
                query = query.Where(d =>
                    d.Subject.ToLower().Contains(searchQuery)
                    || d.DeveloperName.ToLower().Contains(searchQuery)
                    || d.LastModifierUser.FirstName.ToLower().Contains(searchQuery)
                    || d.LastModifierUser.LastName.ToLower().Contains(searchQuery)
                );
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .ApplyPaginationInput(request)
                .Select(EmailTemplateDto.GetProjection())
                .ToArrayAsync(cancellationToken);

            return new QueryResponseDto<ListResultDto<EmailTemplateDto>>(
                new ListResultDto<EmailTemplateDto>(items, total)
            );
        }
    }
}
