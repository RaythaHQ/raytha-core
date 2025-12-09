using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.Entities;
using CSharpVitamins;
using FluentValidation;
using Mediator;

namespace App.Application.UserGroups.Commands;

public class CreateUserGroup
{
    public record Command : LoggableRequest<CommandResponseDto<ShortGuid>>
    {
        public string Label { get; init; } = null!;
        public string DeveloperName { get; init; } = null!;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x.Label).NotEmpty();
            RuleFor(x => x.DeveloperName)
                .Must(StringExtensions.IsValidDeveloperName)
                .WithMessage("Invalid developer name.");
            RuleFor(x => x.DeveloperName)
                .Must(
                    (request, developerName) =>
                    {
                        var entity = db
                            .UserGroups.AsNoTracking()
                            .FirstOrDefault(p =>
                                p.DeveloperName == request.DeveloperName.ToDeveloperName()
                            );
                        return !(entity != null);
                    }
                )
                .WithMessage("A user group with that developer name already exists.");
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
            UserGroup entity = new UserGroup
            {
                Label = request.Label,
                DeveloperName = request.DeveloperName.ToDeveloperName(),
            };

            _db.UserGroups.Add(entity);

            await _db.SaveChangesAsync(cancellationToken);
            return new CommandResponseDto<ShortGuid>(entity.Id);
        }
    }
}
