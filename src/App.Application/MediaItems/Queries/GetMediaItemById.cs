using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.MediaItems.Queries;

public class GetMediaItemById
{
    public record Query : GetEntityByIdInputDto, IRequest<IQueryResponseDto<MediaItemDto>> { }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<MediaItemDto>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<MediaItemDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var entity = _db.MediaItems.AsNoTracking().FirstOrDefault(p => p.Id == request.Id.Guid);

            if (entity == null)
                throw new NotFoundException("MediaItem", request.Id);

            return new QueryResponseDto<MediaItemDto>(MediaItemDto.GetProjection(entity));
        }
    }
}
