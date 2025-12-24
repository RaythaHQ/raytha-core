using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Utils;
using App.Domain.ValueObjects;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace App.Application.AuditLogs.Queries;

public class GetAuditLogs
{
    public record Query
        : GetPagedEntitiesInputDto,
            IRequest<IQueryResponseDto<ListResultDto<AuditLogDto>>>
    {
        public DateTime? StartDateAsUtc { get; init; }
        public DateTime? EndDateAsUtc { get; init; }
        public string Category { get; init; } = null!;
        public ShortGuid? EntityId { get; init; }
        public string EmailAddress { get; init; } = null!;
        public override string OrderBy { get; init; } = $"CreationTime {SortOrder.Descending}";
    }

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.EmailAddress)
                .EmailAddress()
                .When(p => !string.IsNullOrEmpty(p.EmailAddress));
        }
    }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<ListResultDto<AuditLogDto>>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<ListResultDto<AuditLogDto>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var query = _db.AuditLogs.AsNoTracking().AsQueryable();

            if (request.StartDateAsUtc.HasValue)
                query = query.Where(p => p.CreationTime >= request.StartDateAsUtc);

            if (request.EndDateAsUtc.HasValue)
            {
                var endOfEndDateAsUtc = request.EndDateAsUtc.Value.AddDays(1).AddMilliseconds(-1);
                query = query.Where(p => p.CreationTime <= endOfEndDateAsUtc);
            }

            if (request.EntityId.HasValue && request.EntityId != Guid.Empty)
                query = query.Where(p => p.EntityId == request.EntityId.Value);

            if (!string.IsNullOrEmpty(request.EmailAddress))
            {
                query = query.Where(d => d.UserEmail.ToLower() == request.EmailAddress.ToLower());
            }

            if (!string.IsNullOrEmpty(request.Category))
            {
                query = query.Where(d => d.Category == request.Category);
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .ApplyPaginationInput(request)
                .Select(AuditLogDto.GetProjection())
                .ToArrayAsync(cancellationToken);

            return new QueryResponseDto<ListResultDto<AuditLogDto>>(
                new ListResultDto<AuditLogDto>(items, total)
            );
        }
    }
}
