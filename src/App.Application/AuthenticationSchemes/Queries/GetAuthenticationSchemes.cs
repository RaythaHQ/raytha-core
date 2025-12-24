using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.ValueObjects;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.AuthenticationSchemes.Queries;

public class GetAuthenticationSchemes
{
    public record Query
        : GetPagedEntitiesInputDto,
            IRequest<IQueryResponseDto<ListResultDto<AuthenticationSchemeDto>>>
    {
        public bool? IsEnabledForAdmins { get; init; }
        public bool? IsEnabledForUsers { get; init; }
        public override string OrderBy { get; init; } = $"Label {SortOrder.Ascending}";
    }

    public class Handler
        : IRequestHandler<Query, IQueryResponseDto<ListResultDto<AuthenticationSchemeDto>>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<ListResultDto<AuthenticationSchemeDto>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var query = _db
                .AuthenticationSchemes.AsNoTracking()
                .Include(p => p.LastModifierUser)
                .AsQueryable();

            if (request.IsEnabledForUsers.HasValue)
                query = query.Where(p => p.IsEnabledForUsers);

            if (request.IsEnabledForAdmins.HasValue)
                query = query.Where(p => p.IsEnabledForAdmins);

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
                .Select(AuthenticationSchemeDto.GetProjection())
                .ToArrayAsync(cancellationToken);

            return new QueryResponseDto<ListResultDto<AuthenticationSchemeDto>>(
                new ListResultDto<AuthenticationSchemeDto>(items, total)
            );
        }
    }
}
