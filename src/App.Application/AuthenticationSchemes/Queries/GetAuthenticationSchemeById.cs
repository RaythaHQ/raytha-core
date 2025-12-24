using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.AuthenticationSchemes.Queries;

public class GetAuthenticationSchemeById
{
    public record Query
        : GetEntityByIdInputDto,
            IRequest<IQueryResponseDto<AuthenticationSchemeDto>> { }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<AuthenticationSchemeDto>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<AuthenticationSchemeDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var entity = await _db.AuthenticationSchemes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.Id.Guid, cancellationToken);

            if (entity == null)
                throw new NotFoundException("Authentication Scheme", request.Id);

            return new QueryResponseDto<AuthenticationSchemeDto>(
                AuthenticationSchemeDto.GetProjection(entity)
            );
        }
    }
}
