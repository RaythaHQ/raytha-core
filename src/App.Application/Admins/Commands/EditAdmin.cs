using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Admins.Commands;

public class EditAdmin
{
    public record Command : LoggableEntityRequest<CommandResponseDto<ShortGuid>>
    {
        public string FirstName { get; init; } = null!;
        public string LastName { get; init; } = null!;
        public string EmailAddress { get; init; } = null!;
        public IEnumerable<ShortGuid> Roles { get; init; } = null!;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x.FirstName).NotEmpty();
            RuleFor(x => x.LastName).NotEmpty();
            RuleFor(x => x.Roles).NotEmpty();
            RuleFor(x => x.EmailAddress).NotEmpty().EmailAddress();
            RuleFor(x => x)
                .Custom(
                    (request, context) =>
                    {
                        var entity = db
                            .Users.AsNoTracking()
                            .FirstOrDefault(p => p.Id == request.Id.Guid);

                        if (entity == null)
                            throw new NotFoundException("Admin", request.Id);

                        if (request.EmailAddress.ToLower() != entity.EmailAddress.ToLower())
                        {
                            var emailAddressToCheck = request.EmailAddress.ToLower();
                            var doesAnotherEmailExist = db
                                .Users.AsNoTracking()
                                .Any(p => p.EmailAddress.ToLower() == emailAddressToCheck);
                            if (doesAnotherEmailExist)
                            {
                                context.AddFailure(
                                    "EmailAddress",
                                    "Another user with this email address already exists"
                                );
                                return;
                            }
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
            var entity = _db
                .Users.Include(p => p.Roles)
                .First(p => p.Id == request.Id.Guid && p.IsAdmin);

            entity.FirstName = request.FirstName;
            entity.LastName = request.LastName;
            entity.EmailAddress = request.EmailAddress;

            var currentRoleIds = entity.Roles.Select(p => (ShortGuid)p.Id);

            var rolesToAddIds = request.Roles.Except(currentRoleIds);
            var rolesToDeleteIds = currentRoleIds.Except(request.Roles);

            foreach (var roleToAddId in rolesToAddIds)
            {
                var roleToAdd = _db.Roles.First(p => p.Id == roleToAddId.Guid);
                entity.Roles.Add(roleToAdd);
            }

            foreach (var roleToDeleteId in rolesToDeleteIds)
            {
                entity.Roles.Remove(entity.Roles.First(p => p.Id == roleToDeleteId.Guid));
            }

            await _db.SaveChangesAsync(cancellationToken);

            return new CommandResponseDto<ShortGuid>(request.Id);
        }
    }
}
