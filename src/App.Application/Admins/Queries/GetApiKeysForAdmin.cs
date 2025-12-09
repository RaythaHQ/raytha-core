using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.ValueObjects;
using CSharpVitamins;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Admins.Queries;

public class GetApiKeysForAdmin
{
    public record Query
        : GetPagedEntitiesInputDto,
            IRequest<IQueryResponseDto<ListResultDto<ApiKeyDto>>>
    {
        public ShortGuid UserId { get; init; }
        public override string OrderBy { get; init; } = $"CreationTime {SortOrder.ASCENDING}";
    }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<ListResultDto<ApiKeyDto>>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<ListResultDto<ApiKeyDto>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var query = _db
                .ApiKeys.AsNoTracking()
                .Where(p => p.UserId == request.UserId.Guid)
                .AsQueryable();

            var total = query.Count();
            var items = query
                .ApplyPaginationInput(request)
                .Select(ApiKeyDto.GetProjection())
                .ToArray();

            return new QueryResponseDto<ListResultDto<ApiKeyDto>>(
                new ListResultDto<ApiKeyDto>(items, total)
            );
        }
    }
}
