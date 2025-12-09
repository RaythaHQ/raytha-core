using System.Text.Json.Serialization;
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

public class CreateUser
{
    public record Command : LoggableRequest<CommandResponseDto<ShortGuid>>
    {
        public string FirstName { get; init; } = null!;
        public string LastName { get; init; } = null!;
        public string EmailAddress { get; init; } = null!;

        [JsonIgnore]
        public string Password { get; init; } = null!;

        [JsonIgnore]
        public string ConfirmPassword { get; init; } = null!;
        public bool SendEmail { get; init; } = false;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x.FirstName).NotEmpty();
            RuleFor(x => x.LastName).NotEmpty();
            RuleFor(x => x.EmailAddress).NotEmpty().EmailAddress();
            RuleFor(x => x)
                .Custom(
                    (request, context) =>
                    {
                        if (
                            string.IsNullOrEmpty(request.Password)
                            || request.Password.Length
                                < PasswordUtility.PASSWORD_MIN_CHARACTER_LENGTH
                        )
                        {
                            context.AddFailure(
                                "Password",
                                $"Password must be at least {PasswordUtility.PASSWORD_MIN_CHARACTER_LENGTH} characters."
                            );
                            return;
                        }

                        if (request.Password != request.ConfirmPassword)
                        {
                            context.AddFailure(
                                "ConfirmPassword",
                                "Confirm Password did not match."
                            );
                            return;
                        }

                        var emailAndPasswordScheme = db
                            .AuthenticationSchemes.AsNoTracking()
                            .First(p =>
                                p.DeveloperName
                                == AuthenticationSchemeType.EmailAndPassword.DeveloperName
                            );
                        if (!emailAndPasswordScheme.IsEnabledForUsers)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "Email and password authentication scheme must be enabled for users to self register."
                            );
                            return;
                        }

                        var entity = db
                            .Users.AsNoTracking()
                            .FirstOrDefault(p =>
                                p.EmailAddress.ToLower() == request.EmailAddress.ToLower()
                            );

                        if (entity != null)
                        {
                            context.AddFailure(
                                "EmailAddress",
                                "Another user with this email address already exists."
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
            var defaultToEmailAndPasswordScheme = _db.AuthenticationSchemes.First(p =>
                p.DeveloperName == AuthenticationSchemeType.EmailAndPassword.DeveloperName
            );
            var salt = PasswordUtility.RandomSalt();
            var newUserId = Guid.NewGuid();
            var entity = new User
            {
                Id = newUserId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                EmailAddress = request.EmailAddress,
                IsActive = true,
                IsAdmin = false,
                Salt = salt,
                PasswordHash = PasswordUtility.Hash(request.Password, salt),
                SsoId = (ShortGuid)newUserId,
                AuthenticationSchemeId = defaultToEmailAndPasswordScheme.Id,
            };

            entity.AddDomainEvent(
                new UserCreatedEvent(entity, request.SendEmail, request.Password)
            );

            _db.Users.Add(entity);

            await _db.SaveChangesAsync(cancellationToken);
            return new CommandResponseDto<ShortGuid>(entity.Id);
        }
    }
}
