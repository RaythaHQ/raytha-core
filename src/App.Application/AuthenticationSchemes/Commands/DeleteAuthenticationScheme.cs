using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using CSharpVitamins;
using FluentValidation;
using Mediator;

namespace App.Application.AuthenticationSchemes.Commands;

public class DeleteAuthenticationScheme
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
                            .AuthenticationSchemes.AsNoTracking()
                            .FirstOrDefault(p => p.Id == request.Id.Guid);
                        if (entity == null)
                            throw new NotFoundException("Authentication Scheme", request.Id);

                        if (entity.IsBuiltInAuth)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "You cannot remove this built-in authentication scheme."
                            );
                            return;
                        }

                        var onlyOneAdminAuthLeft =
                            db.AuthenticationSchemes.AsNoTracking().Count(p => p.IsEnabledForAdmins)
                            == 1;
                        if (entity.IsEnabledForAdmins && onlyOneAdminAuthLeft)
                        {
                            context.AddFailure(
                                "IsEnabledForAdmins",
                                "You must have at least 1 authentication scheme enabled for administrators."
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
            var entity = _db.AuthenticationSchemes.First(p => p.Id == request.Id.Guid);

            _db.AuthenticationSchemes.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return new CommandResponseDto<ShortGuid>(request.Id);
        }
    }
}
