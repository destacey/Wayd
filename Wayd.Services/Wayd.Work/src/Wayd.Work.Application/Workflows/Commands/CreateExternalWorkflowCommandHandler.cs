using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.Workflows.Dtos;
using Wayd.Work.Application.WorkProcesses.Commands;
using Wayd.Work.Domain.Interfaces;

namespace Wayd.Work.Application.Workflows.Commands;

public sealed class CreateExternalWorkflowCommandHandler(IWorkDbContext workDbContext, ILogger<CreateExternalWorkProcessCommandHandler> logger) : ICommandHandler<CreateExternalWorkflowCommand, Guid>
{
    private const string AppRequestName = nameof(CreateExternalWorkflowCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<CreateExternalWorkProcessCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateExternalWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workStatuses = await _workDbContext.WorkStatuses.Where(ws => request.ExternalWorkTypeWorkflow.WorkflowStates.Select(ws => ws.StatusName).Contains(ws.Name)).ToArrayAsync(cancellationToken);

        List<ICreateWorkflowScheme> schemes = new(request.ExternalWorkTypeWorkflow.WorkflowStates.Count);
        foreach (var ws in request.ExternalWorkTypeWorkflow.WorkflowStates)
        {
            var workStatus = workStatuses.FirstOrDefault(s => s.Name == ws.StatusName);
            if (workStatus is null)
            {
                _logger.LogError("{AppRequestName}: work status {WorkStatusName} not found.", AppRequestName, ws.StatusName);
                return Result.Failure<Guid>($"Work status {ws.StatusName} not found.");
            }

            schemes.Add(CreateWorkflowSchemeDto.Create(workStatus.Id, ws.Category, ws.Order, ws.IsActive));
        }

        var workflow = Workflow.CreateExternal(request.Name, request.Description, schemes);

        _workDbContext.Workflows.Add(workflow);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: created workflow {WorkflowName}.", AppRequestName, workflow.Name);

        return Result.Success(workflow.Id);
    }
}

