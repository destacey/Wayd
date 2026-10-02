using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed class GetIterationMappingsQueryHandler(IWorkDbContext workDbContext, ILogger<GetIterationMappingsQueryHandler> logger) : IQueryHandler<GetIterationMappingsQuery, Dictionary<string, Guid>>
{
    private const string AppRequestName = nameof(GetIterationMappingsQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetIterationMappingsQueryHandler> _logger = logger;

    public async Task<Dictionary<string, Guid>> Handle(GetIterationMappingsQuery request, CancellationToken cancellationToken)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for SystemId {SystemId}.", AppRequestName, request.SystemId);

            return [];
        }
    }
}
