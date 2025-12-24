using Mediator;
using Microsoft.EntityFrameworkCore;
using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;

namespace App.Application.Login.Commands;

public class LoginWithApiKey
{
    public record Command : IRequest<CommandResponseDto<LoginDto>>
    {
        public string ApiKey { get; init; } = null!;
    }

    public class Handler : IRequestHandler<Command, CommandResponseDto<LoginDto>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<CommandResponseDto<LoginDto>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var hashedApiKey = PasswordUtility.Hash(request.ApiKey);
            var entity = await _db.ApiKeys
                .AsNoTracking()
                .Include(p => p.User)
                .ThenInclude(p => p.UserGroups)
                .Include(p => p.User)
                .ThenInclude(p => p.Roles)
                .FirstOrDefaultAsync(p => p.ApiKeyHash == hashedApiKey, cancellationToken);

            if (entity == null)
                throw new InvalidApiKeyException("Api key was not found.");

            if (!entity.User.IsActive || !entity.User.IsAdmin)
                throw new InvalidApiKeyException(
                    "Api key is not connected to an active administrator"
                );

            return new CommandResponseDto<LoginDto>(LoginDto.GetProjection(entity.User));
        }
    }
}
