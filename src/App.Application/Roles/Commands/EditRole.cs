using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Domain.Entities;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Roles.Commands;

public class EditRole
{
    public record Command : LoggableEntityRequest<CommandResponseDto<ShortGuid>>
    {
        public string Label { get; init; } = null!;
        public IEnumerable<string> SystemPermissions { get; init; } = null!;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x.Id)
                .Must(id =>
                {
                    var entity = db.Roles.AsNoTracking().FirstOrDefault(p => p.Id == id.Guid);
                    return entity == null || entity.DeveloperName != BuiltInRole.SuperAdmin;
                })
                .WithMessage("The Super Admin role cannot be edited.");
            RuleFor(x => x.Label).NotEmpty();
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
            var entity = _db.Roles.FirstOrDefault(p => p.Id == request.Id.Guid);
            if (entity == null)
                throw new NotFoundException("Role", request.Id);

            // Prevent any edits to the Super Admin role
            if (entity.DeveloperName == BuiltInRole.SuperAdmin)
                throw new InvalidOperationException("The Super Admin role cannot be edited.");

            entity.Label = request.Label;
            entity.SystemPermissions = BuiltInSystemPermission.From(
                request.SystemPermissions.ToArray()
            );

            await _db.SaveChangesAsync(cancellationToken);
            return new CommandResponseDto<ShortGuid>(entity.Id);
        }
    }
}
