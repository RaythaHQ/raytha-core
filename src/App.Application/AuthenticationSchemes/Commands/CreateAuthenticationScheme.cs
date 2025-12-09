using System.Text.Json.Serialization;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.Entities;
using App.Domain.ValueObjects;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.AuthenticationSchemes.Commands;

public class CreateAuthenticationScheme
{
    public record Command : LoggableRequest<CommandResponseDto<ShortGuid>>
    {
        public string Label { get; init; } = null!;
        public string DeveloperName { get; init; } = null!;

        [JsonIgnore]
        public string JwtSecretKey { get; init; } = null!;

        [JsonIgnore]
        public string SamlCertificate { get; init; } = null!;
        public string SignInUrl { get; init; } = null!;
        public string SignOutUrl { get; init; } = null!;
        public string LoginButtonText { get; init; } = null!;
        public string AuthenticationSchemeType { get; init; } = null!;
        public bool JwtUseHighSecurity { get; init; }

        [JsonIgnore]
        public string SamlIdpEntityId { get; init; } = null!;
        public bool IsEnabledForUsers { get; init; }
        public bool IsEnabledForAdmins { get; init; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x.Label).NotEmpty();
            RuleFor(x => x.LoginButtonText).NotEmpty();
            RuleFor(x => x.AuthenticationSchemeType).NotEmpty();
            RuleFor(x => x.SignInUrl)
                .NotEmpty()
                .Must(StringExtensions.IsValidUriFormat)
                .When(p =>
                    p.AuthenticationSchemeType == AuthenticationSchemeType.Jwt.DeveloperName
                    || p.AuthenticationSchemeType == AuthenticationSchemeType.Saml.DeveloperName
                )
                .WithMessage("Sign in url is not a valid url.");
            RuleFor(x => x.SignOutUrl)
                .Must(StringExtensions.IsValidUriFormat)
                .When(p => !string.IsNullOrEmpty(p.SignOutUrl))
                .WithMessage("Sign out url is not a valid url.");
            RuleFor(x => x.JwtSecretKey)
                .NotEmpty()
                .When(p =>
                    p.AuthenticationSchemeType == AuthenticationSchemeType.Jwt.DeveloperName
                );
            RuleFor(x => x.SamlCertificate)
                .NotEmpty()
                .When(p =>
                    p.AuthenticationSchemeType == AuthenticationSchemeType.Saml.DeveloperName
                );
            RuleFor(x => x.SamlIdpEntityId)
                .NotEmpty()
                .When(p =>
                    p.AuthenticationSchemeType == AuthenticationSchemeType.Saml.DeveloperName
                );
            RuleFor(x => x.AuthenticationSchemeType)
                .Must(x =>
                    x == AuthenticationSchemeType.Jwt.DeveloperName
                    || x == AuthenticationSchemeType.Saml.DeveloperName
                )
                .WithMessage("Invalid authentication scheme type.");
            RuleFor(x => x.DeveloperName)
                .Must(StringExtensions.IsValidDeveloperName)
                .WithMessage("Invalid developer name.");
            RuleFor(x => x.DeveloperName)
                .NotEmpty()
                .Must(
                    (request, developerName) =>
                    {
                        var isDeveloperNameAlreadyExist = db
                            .AuthenticationSchemes.AsNoTracking()
                            .Any(p => p.DeveloperName == request.DeveloperName.ToDeveloperName());
                        return !isDeveloperNameAlreadyExist;
                    }
                )
                .WithMessage("An authentication scheme with that developer name already exists.");
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
            var entity = new AuthenticationScheme
            {
                Label = request.Label,
                DeveloperName = request.DeveloperName.ToDeveloperName(),
                JwtSecretKey = request.JwtSecretKey,
                JwtUseHighSecurity = request.JwtUseHighSecurity,
                SamlCertificate = request.SamlCertificate,
                SamlIdpEntityId = request.SamlIdpEntityId,
                LoginButtonText = request.LoginButtonText,
                SignInUrl = request.SignInUrl,
                SignOutUrl = request.SignOutUrl,
                AuthenticationSchemeType = AuthenticationSchemeType.From(
                    request.AuthenticationSchemeType
                ),
                IsEnabledForUsers = request.IsEnabledForUsers,
                IsEnabledForAdmins = request.IsEnabledForAdmins,
                IsBuiltInAuth = false,
            };

            _db.AuthenticationSchemes.Add(entity);

            await _db.SaveChangesAsync(cancellationToken);

            return new CommandResponseDto<ShortGuid>(entity.Id);
        }
    }
}
