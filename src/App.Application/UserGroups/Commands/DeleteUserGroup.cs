using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.UserGroups.Commands;

public class DeleteUserGroup
{
    public record Command : LoggableEntityRequest<CommandResponseDto<ShortGuid>> { }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x)
                .Custom(
                    (request, context) =>
                    {
                        var entity = db
                            .UserGroups.AsNoTracking()
                            .Include(p => p.Users)
                            .FirstOrDefault(p => p.Id == request.Id.Guid);
                        if (entity == null)
                            throw new NotFoundException("UserGroup", request.Id);

                        if (entity.Users.Any())
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "Users are still assigned to this group. Unassign these users before deleting this group."
                            );
                            return;
                        }
                    }
                );
        }
    }

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
            var entity = _db.UserGroups.Include(p => p.Users).First(p => p.Id == request.Id.Guid);

            _db.UserGroups.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return new CommandResponseDto<ShortGuid>(request.Id);
        }
    }
}
