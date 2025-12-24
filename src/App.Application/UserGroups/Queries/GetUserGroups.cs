using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.UserGroups.Queries;

public class GetUserGroups
{
    public record Query
        : GetPagedEntitiesInputDto,
            IRequest<IQueryResponseDto<ListResultDto<UserGroupDto>>>
    {
        public override string OrderBy { get; init; } = $"Label {SortOrder.ASCENDING}";
    }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<ListResultDto<UserGroupDto>>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<ListResultDto<UserGroupDto>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var query = _db.UserGroups.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(request.Search))
            {
                var searchQuery = request.Search.ToLower();
                query = query.Where(d =>
                    d.Label.ToLower().Contains(searchQuery)
                    || d.DeveloperName.ToLower().Contains(searchQuery)
                );
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .ApplyPaginationInput(request)
                .Select(UserGroupDto.GetProjection())
                .ToArrayAsync(cancellationToken);

            return new QueryResponseDto<ListResultDto<UserGroupDto>>(
                new ListResultDto<UserGroupDto>(items, total)
            );
        }
    }
}
