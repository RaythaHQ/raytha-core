using System.Text.Json.Serialization;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;

namespace App.Application.OrganizationSettings.Commands;

public class EditSmtp
{
    public record Command : LoggableRequest<CommandResponseDto<ShortGuid>>
    {
        public bool SmtpOverrideSystem { get; init; }

        [JsonIgnore]
        public string SmtpHost { get; init; } = null!;

        [JsonIgnore]
        public int? SmtpPort { get; init; }

        [JsonIgnore]
        public string SmtpUsername { get; init; } = null!;

        [JsonIgnore]
        public string SmtpPassword { get; init; } = null!;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IEmailerConfiguration emailerConfiguration)
        {
            RuleFor(x => x.SmtpOverrideSystem)
                .Equal(true)
                .When(p => emailerConfiguration.IsMissingSmtpEnvVars())
                .WithMessage(
                    "The server administrator did not set SMTP environment variables, so you must override the system defaults."
                );
            RuleFor(x => x.SmtpHost).NotEmpty().When(p => p.SmtpOverrideSystem);
            RuleFor(x => x.SmtpPort)
                .NotNull()
                .GreaterThan(0)
                .LessThanOrEqualTo(65535)
                .When(p => p.SmtpOverrideSystem);
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
            var entity = await _db.OrganizationSettings.FirstAsync(cancellationToken);

            entity.SmtpOverrideSystem = request.SmtpOverrideSystem;
            entity.SmtpHost = request.SmtpHost;
            entity.SmtpPort = request.SmtpPort;
            entity.SmtpUsername = request.SmtpUsername;
            entity.SmtpPassword = request.SmtpPassword;

            await _db.SaveChangesAsync(cancellationToken);

            // Invalidate cache after changes
            _cache.InvalidateOrganizationSettings();

            return new CommandResponseDto<ShortGuid>(entity.Id);
        }
    }
}
