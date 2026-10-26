using System.Linq.Expressions;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The workflow an intake request moves through, with its statuses, for drawing its history on; or null when
/// the reader cannot see the request.
/// </summary>
/// <remarks>
/// Read through the request so its readers need no workflow administration permission. The workflow's shape is
/// not sensitive: anyone who can see the request's status can see the stages around it.
/// </remarks>
public sealed record GetIntakeRequestStatusWorkflowQuery : IQuery<StatusWorkflowDetailsDto?>
{
    public GetIntakeRequestStatusWorkflowQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<IntakeRequest>();
    }

    public Expression<Func<IntakeRequest, bool>> IdOrKeyFilter { get; }
}

public sealed class GetIntakeRequestStatusWorkflowQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IStatusWorkflowDetailsReader detailsReader,
    ITransitionConditionChecker conditionChecker)
    : IQueryHandler<GetIntakeRequestStatusWorkflowQuery, StatusWorkflowDetailsDto?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IStatusWorkflowDetailsReader _detailsReader = detailsReader;
    private readonly ITransitionConditionChecker _conditionChecker = conditionChecker;

    public async Task<StatusWorkflowDetailsDto?> Handle(GetIntakeRequestStatusWorkflowQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var workflowId = await _dbContext.IntakeRequests
            .Where(request.IdOrKeyFilter)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .Select(r => (Guid?)r.StatusWorkflowId)
            .FirstOrDefaultAsync(cancellationToken);

        return workflowId is { } id
            ? await _detailsReader.Read(w => w.Id == id, _conditionChecker.IsAvailable, cancellationToken)
            : null;
    }
}
