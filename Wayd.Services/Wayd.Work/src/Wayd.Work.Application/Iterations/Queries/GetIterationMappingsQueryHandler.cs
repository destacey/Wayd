using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed class GetIterationMappingsQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<GetIterationMappingsQuery, Dictionary<string, Guid>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<Dictionary<string, Guid>> Handle(GetIterationMappingsQuery request, CancellationToken cancellationToken)
    {
        var mappings = await _workDbContext.Iterations
            .Where(i => i.OwnershipInfo.Ownership == Ownership.Managed
                && i.OwnershipInfo.Connector == request.Connector
                && i.OwnershipInfo.SystemId == request.SystemId)
            .Select(i => new
            {
                ExternalIterationId = i.OwnershipInfo.ExternalId!,
                InternalIterationId = i.Id
            })
            .ToDictionaryAsync(i => i.ExternalIterationId, i => i.InternalIterationId, cancellationToken);

        return mappings;
    }
}
