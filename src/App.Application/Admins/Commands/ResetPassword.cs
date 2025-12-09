using System.Text.Json.Serialization;
using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.Events;
using App.Domain.ValueObjects;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Admins.Commands;

public class ResetPassword
{
    public record Command : LoggableEntityRequest<CommandResponseDto<ShortGuid>>
    {
        [JsonIgnore]
        public string NewPassword { get; init; } = null!;

        [JsonIgnore]
        public string ConfirmNewPassword { get; init; } = null!;
        public bool SendEmail { get; init; } = true;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x)
                .Custom(
                    (request, context) =>
                    {
                        if (
                            request.NewPassword.Length
                            < PasswordUtility.PASSWORD_MIN_CHARACTER_LENGTH
                        )
                        {
                            context.AddFailure(
                                "NewPassword",
                                $"Password must be at least {PasswordUtility.PASSWORD_MIN_CHARACTER_LENGTH} chracters."
                            );
                            return;
                        }

                        if (request.NewPassword != request.ConfirmNewPassword)
                        {
                            context.AddFailure(
                                "ConfirmNewPassword",
                                "Confirm Password did not match."
                            );
                            return;
                        }

                        var authScheme = db
                            .AuthenticationSchemes.AsNoTracking()
                            .First(p =>
                                p.AuthenticationSchemeType
                                == AuthenticationSchemeType.EmailAndPassword.DeveloperName
                            );

                        if (!authScheme.IsEnabledForAdmins)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "Authentication scheme disabled for administrators."
                            );
                            return;
                        }

                        var entity = db
                            .Users.AsNoTracking()
                            .Include(p => p.AuthenticationScheme)
                            .FirstOrDefault(p => p.Id == request.Id.Guid && p.IsAdmin);

                        if (entity == null)
                            throw new NotFoundException("Admin", request.Id);

                        if (!entity.IsActive)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "User has been deactivated."
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
            var entity = _db
                .Users.Include(p => p.AuthenticationScheme)
                .First(p => p.Id == request.Id.Guid && p.IsAdmin);

            var salt = PasswordUtility.RandomSalt();
            entity.Salt = salt;
            entity.PasswordHash = PasswordUtility.Hash(request.NewPassword, salt);

            entity.AddDomainEvent(
                new AdminPasswordResetEvent(entity, request.SendEmail, request.NewPassword)
            );

            await _db.SaveChangesAsync(cancellationToken);
            return new CommandResponseDto<ShortGuid>(request.Id);
        }
    }
}
