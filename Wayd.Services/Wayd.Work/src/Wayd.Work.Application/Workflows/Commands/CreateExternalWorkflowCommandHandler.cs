using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workflows.Commands;

public sealed class CreateExternalWorkflowCommandHandler(IWorkDbContext workDbContext, ILogger<CreateExternalWorkflowCommandHandler> logger) : ICommandHandler<CreateExternalWorkflowCommand, Guid>
{
    private const string AppRequestName = nameof(CreateExternalWorkflowCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<CreateExternalWorkflowCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateExternalWorkflowCommand request, CancellationToken cancellationToken)
    {
        var statusNames = request.ExternalWorkTypeWorkflow.WorkflowStates.Select(ws => ws.StatusName).ToList();
        var workStatuses = await _workDbContext.WorkStatuses.Where(ws => statusNames.Contains(ws.Name)).ToArrayAsync(cancellationToken);

        var workflowResult = ExternalWorkflowBuilder.Build(request.Name, request.Description, request.ExternalWorkTypeWorkflow, workStatuses);
        if (workflowResult.IsFailure)
        {
            _logger.LogError("{AppRequestName}: {Error}", AppRequestName, workflowResult.Error);
            return workflowResult.ConvertFailure<Guid>();
        }

        var workflow = workflowResult.Value;
        _workDbContext.Workflows.Add(workflow);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: created workflow {WorkflowName}.", AppRequestName, workflow.Name);

        return Result.Success(workflow.Id);
    }
}
