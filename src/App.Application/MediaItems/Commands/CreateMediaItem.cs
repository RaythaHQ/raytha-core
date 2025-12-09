using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.Entities;
using CSharpVitamins;
using FluentValidation;
using Mediator;

namespace App.Application.MediaItems.Commands;

public class CreateMediaItem
{
    public record Command : LoggableRequest<CommandResponseDto<ShortGuid>>
    {
        public ShortGuid Id { get; init; }
        public string FileName { get; init; } = null!;
        public long Length { get; init; }
        public string ContentType { get; init; } = null!;
        public string FileStorageProvider { get; init; } = null!;
        public string ObjectKey { get; init; } = null!;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db, IFileStorageProviderSettings fileStorageProviderSettings)
        {
            RuleFor(x => x.FileName).NotEmpty();
            RuleFor(x => x.ContentType).NotEmpty();
            RuleFor(x => x.Length).GreaterThan(0);
            RuleFor(x => x.ObjectKey).NotEmpty();
            RuleFor(x => x.FileStorageProvider).NotEmpty();
            RuleFor(x => x)
                .Custom(
                    (request, context) =>
                    {
                        if (
                            !FileStorageUtility.IsAllowedMimeType(
                                request.ContentType,
                                fileStorageProviderSettings.AllowedMimeTypes
                            )
                        )
                        {
                            context.AddFailure("ContentType", "File type is not allowed.");
                            return;
                        }

                        if (request.Length > fileStorageProviderSettings.MaxFileSize)
                        {
                            context.AddFailure(
                                "Length",
                                $"File size exceeds maximum allowed size of {fileStorageProviderSettings.MaxFileSize} bytes."
                            );
                            return;
                        }

                        var entity = db
                            .MediaItems.AsNoTracking()
                            .FirstOrDefault(p => p.Id == request.Id.Guid);
                        if (entity != null)
                        {
                            context.AddFailure("Id", "A media item with this ID already exists.");
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
            var entity = new MediaItem
            {
                Id = request.Id.Guid,
                FileName = request.FileName,
                Length = request.Length,
                ContentType = request.ContentType,
                FileStorageProvider = request.FileStorageProvider,
                ObjectKey = request.ObjectKey,
            };

            _db.MediaItems.Add(entity);

            await _db.SaveChangesAsync(cancellationToken);
            return new CommandResponseDto<ShortGuid>(entity.Id);
        }
    }
}
