using CSharpVitamins;
using Mediator;
using Microsoft.EntityFrameworkCore;
using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;

namespace App.Application.Admins.Commands;

public class DeleteApiKey
{
    public record Command : LoggableEntityRequest<CommandResponseDto<ShortGuid>> { }

    public class Handler : IRequestHandler<Command, CommandResponseDto<ShortGuid>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<CommandResponseDto<ShortGuid>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var entity = await _db.ApiKeys.FirstOrDefaultAsync(
                p => p.Id == request.Id.Guid,
                cancellationToken
            );
            if (entity == null)
                throw new NotFoundException("Api Key", request.Id);

            _db.ApiKeys.Remove(entity);

            await _db.SaveChangesAsync(cancellationToken);

            return new CommandResponseDto<ShortGuid>(request.Id);
        }
    }
}
