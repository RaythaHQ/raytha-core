using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.Entities;
using App.Domain.Events;
using App.Domain.ValueObjects;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Login.Commands;

public class BeginLoginWithMagicLink
{
    public record Command : LoggableRequest<CommandResponseDto<ShortGuid>>
    {
        public string EmailAddress { get; init; } = null!;
        public string ReturnUrl { get; init; } = null;
        public bool SendEmail { get; init; } = true;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x.EmailAddress).NotEmpty().EmailAddress();
            RuleFor(x => x)
                .Custom(
                    (request, context) =>
                    {
                        var authScheme = db
                            .AuthenticationSchemes.AsNoTracking()
                            .First(p =>
                                p.AuthenticationSchemeType
                                == AuthenticationSchemeType.MagicLink.DeveloperName
                            );

                        if (!authScheme.IsEnabledForUsers && !authScheme.IsEnabledForAdmins)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "Authentication scheme is disabled."
                            );
                            return;
                        }

                        var emailAddress = request.EmailAddress.ToLower().Trim();
                        var entity = db
                            .Users.AsNoTracking()
                            .FirstOrDefault(p => p.EmailAddress.ToLower() == emailAddress);

                        if (entity == null)
                        {
                            context.AddFailure(Constants.VALIDATION_SUMMARY, "User not found.");
                            return;
                        }

                        if (!entity.IsActive)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "User has been deactivated."
                            );
                            return;
                        }

                        if (entity.IsAdmin && !authScheme.IsEnabledForAdmins)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "Authentication scheme disabled for administrators."
                            );
                            return;
                        }

                        if (!entity.IsAdmin && !authScheme.IsEnabledForUsers)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "Authentication scheme disabled for public users."
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
            var authScheme = _db.AuthenticationSchemes.First(p =>
                p.AuthenticationSchemeType == AuthenticationSchemeType.MagicLink.DeveloperName
            );

            var entity = _db
                .Users.Include(p => p.AuthenticationScheme)
                .FirstOrDefault(p =>
                    p.EmailAddress.ToLower() == request.EmailAddress.ToLower().Trim()
                );

            var guid = ShortGuid.NewGuid();
            var otp = new OneTimePassword
            {
                Id = PasswordUtility.Hash(guid),
                IsUsed = false,
                UserId = entity.Id,
                ExpiresAt = DateTime.UtcNow.AddSeconds(authScheme.MagicLinkExpiresInSeconds),
            };

            _db.OneTimePasswords.Add(otp);

            entity.AddDomainEvent(
                new BeginLoginWithMagicLinkEvent(
                    entity,
                    request.SendEmail,
                    guid,
                    request.ReturnUrl,
                    authScheme.MagicLinkExpiresInSeconds
                )
            );

            await _db.SaveChangesAsync(cancellationToken);
            return new CommandResponseDto<ShortGuid>(entity.Id);
        }
    }
}
