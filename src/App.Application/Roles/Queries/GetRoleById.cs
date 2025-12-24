using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Roles.Queries;

public class GetRoleById
{
    public record Query : GetEntityByIdInputDto, IRequest<IQueryResponseDto<RoleDto>> { }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<RoleDto>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<RoleDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var entity = await _db.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.Id.Guid, cancellationToken);

            if (entity == null)
                throw new NotFoundException("Role", request.Id);

            return new QueryResponseDto<RoleDto>(RoleDto.GetProjection(entity));
        }
    }
}
