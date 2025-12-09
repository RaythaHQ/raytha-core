using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.BackgroundTasks.Queries;

public class GetBackgroundTaskById
{
    public record Query : GetEntityByIdInputDto, IRequest<IQueryResponseDto<BackgroundTaskDto>> { }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<BackgroundTaskDto>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<BackgroundTaskDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var entity = _db
                .BackgroundTasks.AsNoTracking()
                .FirstOrDefault(p => p.Id == request.Id.Guid);

            if (entity == null)
                throw new NotFoundException("Background Task", request.Id);

            return new QueryResponseDto<BackgroundTaskDto>(BackgroundTaskDto.GetProjection(entity));
        }
    }
}
