using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.Entities;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Roles.Commands;

public class DeleteRole
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
                            .Roles.AsNoTracking()
                            .Include(p => p.Users)
                            .FirstOrDefault(p => p.Id == request.Id.Guid);
                        if (entity == null)
                            throw new NotFoundException("Role", request.Id);

                        if (entity.Users.Any())
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "Users are still assigned to this role. Unassign these users before deleting this role."
                            );
                            return;
                        }

                        if (entity.DeveloperName == BuiltInRole.SuperAdmin)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                $"You cannot remove the {entity.Label} role."
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
            var entity = _db.Roles.Include(p => p.Users).First(p => p.Id == request.Id.Guid);

            _db.Roles.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return new CommandResponseDto<ShortGuid>(request.Id);
        }
    }
}
