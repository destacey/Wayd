using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>The record types of one owner type, or of every available one, by owner then name.</summary>
public sealed record GetRecordTypesQuery(string? OwnerType) : IQuery<List<RecordTypeDto>>;

public sealed class GetRecordTypesQueryHandler(
    IStatusWorkflowDbContext dbContext,
    IWorkflowOwnerAvailability ownerAvailability)
    : IQueryHandler<GetRecordTypesQuery, List<RecordTypeDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<List<RecordTypeDto>> Handle(GetRecordTypesQuery request, CancellationToken cancellationToken)
    {
        var unavailable = (await _ownerAvailability.Unavailable(cancellationToken)).ToArray();

        var query = _dbContext.RecordTypes.AsNoTracking().Where(r => !unavailable.Contains(r.OwnerType));
        if (!string.IsNullOrWhiteSpace(request.OwnerType))
        {
            query = query.Where(r => r.OwnerType == request.OwnerType);
        }

        var recordTypes = await query
            .Select(r => new { r.Id, r.Key, r.Name, r.OwnerType, r.Ownership, r.ExternalKey, r.IsActive })
            .ToListAsync(cancellationToken);

        return
        [
            .. recordTypes
                .Select(r => new RecordTypeDto
                {
                    Id = r.Id,
                    Key = r.Key,
                    Name = r.Name,
                    Owner = GetStatusWorkflowsQueryHandler.DescribeOwner(r.OwnerType),
                    Ownership = r.Ownership.ToString(),
                    ExternalKey = r.ExternalKey,
                    IsActive = r.IsActive,
                })
                .OrderBy(r => r.Owner.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Key),
        ];
    }
}
