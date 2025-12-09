using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.Entities;
using CSharpVitamins;
using FluentValidation;
using Mediator;

namespace App.Application.Roles.Commands;

public class CreateRole
{
    public record Command : LoggableRequest<CommandResponseDto<ShortGuid>>
    {
        public string Label { get; init; } = null!;
        public string DeveloperName { get; init; } = null!;
        public IEnumerable<string> SystemPermissions { get; init; } = null!;
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
                            .Roles.AsNoTracking()
                            .FirstOrDefault(p =>
                                p.DeveloperName == request.DeveloperName.ToDeveloperName()
                            );
                        return !(entity != null);
                    }
                )
                .WithMessage("A role with that developer name already exists.");
            RuleFor(x => x.SystemPermissions)
                .Must(permissions =>
                {
                    var permissionsList = permissions?.ToList() ?? new List<string>();
                    var hasSystemSettings = permissionsList.Contains(
                        BuiltInSystemPermission.MANAGE_SYSTEM_SETTINGS_PERMISSION
                    );
                    var hasAdministrators = permissionsList.Contains(
                        BuiltInSystemPermission.MANAGE_ADMINISTRATORS_PERMISSION
                    );
                    // Both must be selected together or neither
                    return hasSystemSettings == hasAdministrators;
                })
                .WithMessage(
                    "Manage System Settings and Manage Administrators permissions must be selected together."
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
            var builtInSystemPermissions = BuiltInSystemPermission.From(
                request.SystemPermissions.ToArray()
            );

            Role entity = new Role
            {
                Label = request.Label,
                DeveloperName = request.DeveloperName.ToDeveloperName(),
                SystemPermissions = builtInSystemPermissions,
            };

            _db.Roles.Add(entity);

            await _db.SaveChangesAsync(cancellationToken);
            return new CommandResponseDto<ShortGuid>(entity.Id);
        }
    }
}
