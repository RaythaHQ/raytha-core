using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Login.Queries;

public class GetUserForAuthenticationById
{
    public record Query : GetEntityByIdInputDto, IRequest<IQueryResponseDto<LoginDto>> { }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<LoginDto>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<LoginDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var entity = await _db.Users
                .AsNoTracking()
                .Include(p => p.Roles)
                .Include(p => p.UserGroups)
                .Include(p => p.AuthenticationScheme)
                .FirstOrDefaultAsync(p => p.Id == request.Id.Guid, cancellationToken);

            if (entity == null)
                throw new NotFoundException("User", request.Id);

            return new QueryResponseDto<LoginDto>(LoginDto.GetProjection(entity));
        }
    }
}
