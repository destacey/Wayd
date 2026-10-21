using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

public sealed record GetStatusWorkflowQuery : IQuery<StatusWorkflowDetailsDto?>
{
    public GetStatusWorkflowQuery(IdOrKey idOrKey) => IdOrKeyFilter = idOrKey.CreateFilter<StatusWorkflow>();

    public Expression<Func<StatusWorkflow, bool>> IdOrKeyFilter { get; }
}

public sealed class GetStatusWorkflowQueryHandler(
    IStatusWorkflowDetailsReader detailsReader,
    ITransitionConditionChecker conditionChecker)
    : IQueryHandler<GetStatusWorkflowQuery, StatusWorkflowDetailsDto?>
{
    private readonly IStatusWorkflowDetailsReader _detailsReader = detailsReader;
    private readonly ITransitionConditionChecker _conditionChecker = conditionChecker;

    public Task<StatusWorkflowDetailsDto?> Handle(
        GetStatusWorkflowQuery request, CancellationToken cancellationToken) =>
        _detailsReader.Read(request.IdOrKeyFilter, _conditionChecker.IsAvailable, cancellationToken);
}
