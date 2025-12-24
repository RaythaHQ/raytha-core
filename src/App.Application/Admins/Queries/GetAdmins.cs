using System.Data;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.ValueObjects;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Admins.Queries;

public class GetAdmins
{
    public record Query
        : GetPagedEntitiesInputDto,
            IRequest<IQueryResponseDto<ListResultDto<AdminDto>>>
    {
        public override string OrderBy { get; init; } = $"LastLoggedInTime {SortOrder.DESCENDING}";
    }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<ListResultDto<AdminDto>>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<ListResultDto<AdminDto>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var query = _db.Users.AsNoTracking().Include(p => p.Roles).Where(p => p.IsAdmin);

            if (!string.IsNullOrEmpty(request.Search))
            {
                var searchQuery = request.Search.ToLower();
                query = query
                    .Include(p => p.Roles)
                    .Where(d =>
                        d.FirstName.ToLower().Contains(searchQuery)
                        || d.LastName.ToLower().Contains(searchQuery)
                        || d.EmailAddress.ToLower().Contains(searchQuery)
                        || d.Roles.Any(p => p.Label.Contains(searchQuery))
                    );
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .ApplyPaginationInput(request)
                .Select(AdminDto.GetProjection())
                .ToArrayAsync(cancellationToken);

            return new QueryResponseDto<ListResultDto<AdminDto>>(
                new ListResultDto<AdminDto>(items, total)
            );
        }
    }
}
