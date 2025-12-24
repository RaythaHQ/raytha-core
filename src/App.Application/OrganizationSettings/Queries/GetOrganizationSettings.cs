using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.OrganizationSettings.Queries;

public class GetOrganizationSettings
{
    public record Query : IRequest<IQueryResponseDto<OrganizationSettingsDto>> { }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<OrganizationSettingsDto>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<OrganizationSettingsDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var settings = await _db.OrganizationSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            return new QueryResponseDto<OrganizationSettingsDto>(
                OrganizationSettingsDto.GetProjection(settings)
            );
        }
    }
}
