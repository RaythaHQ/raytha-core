using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.UserGroups.Queries;

public class GetUserGroupById
{
    public record Query : GetEntityByIdInputDto, IRequest<IQueryResponseDto<UserGroupDto>> { }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<UserGroupDto>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<UserGroupDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var entity = await _db.UserGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.Id.Guid, cancellationToken);

            if (entity == null)
                throw new NotFoundException("UserGroup", request.Id);

            return new QueryResponseDto<UserGroupDto>(UserGroupDto.GetProjection(entity));
        }
    }
}
