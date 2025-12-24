using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.AuthenticationSchemes.Queries;

public class GetAuthenticationSchemeByName
{
    public record Query : IRequest<IQueryResponseDto<AuthenticationSchemeDto>>
    {
        public string DeveloperName { get; init; } = null!;
    }

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
                .FirstOrDefaultAsync(
                    p => p.DeveloperName == request.DeveloperName.ToDeveloperName(),
                    cancellationToken
                );

            if (entity == null)
                throw new NotFoundException(
                    "Authentication Scheme",
                    request.DeveloperName.ToDeveloperName()
                );

            return new QueryResponseDto<AuthenticationSchemeDto>(
                AuthenticationSchemeDto.GetProjection(entity)
            );
        }
    }
}
