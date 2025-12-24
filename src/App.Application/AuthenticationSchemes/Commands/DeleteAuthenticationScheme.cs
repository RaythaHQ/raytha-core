using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.AuthenticationSchemes.Commands;

public class DeleteAuthenticationScheme
{
    public record Command : LoggableEntityRequest<CommandResponseDto<ShortGuid>> { }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x)
                .CustomAsync(
                    async (request, context, cancellationToken) =>
                    {
                        var entity = await db
                            .AuthenticationSchemes.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.Id == request.Id.Guid, cancellationToken);
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
                            await db
                                .AuthenticationSchemes.AsNoTracking()
                                .CountAsync(p => p.IsEnabledForAdmins, cancellationToken) == 1;
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
        private readonly IOrganizationSettingsCache _cache;

        public Handler(IAppDbContext db, IOrganizationSettingsCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async ValueTask<CommandResponseDto<ShortGuid>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var entity = await _db.AuthenticationSchemes.FirstAsync(
                p => p.Id == request.Id.Guid,
                cancellationToken
            );

            _db.AuthenticationSchemes.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);

            // Invalidate cache after changes
            _cache.InvalidateAuthenticationSchemes();

            return new CommandResponseDto<ShortGuid>(request.Id);
        }
    }
}
