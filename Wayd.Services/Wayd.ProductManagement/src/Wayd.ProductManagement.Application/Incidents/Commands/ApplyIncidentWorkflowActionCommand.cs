using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Commands;

public sealed record ApplyIncidentWorkflowActionCommand(
    Guid IncidentId,
    IncidentWorkflowAction Action,
    string? Reason) : ICommand, IRequireLinkedEmployee;

public sealed class ApplyIncidentWorkflowActionCommandValidator : AbstractValidator<ApplyIncidentWorkflowActionCommand>
{
    public ApplyIncidentWorkflowActionCommandValidator()
    {
        RuleFor(c => c.IncidentId).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(1024);
    }
}

public sealed class ApplyIncidentWorkflowActionCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<ApplyIncidentWorkflowActionCommandHandler> logger)
    : ICommandHandler<ApplyIncidentWorkflowActionCommand>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ApplyIncidentWorkflowActionCommandHandler> _logger = logger;

    public async Task<Result> Handle(ApplyIncidentWorkflowActionCommand request, CancellationToken cancellationToken)
    {
        var incident = await _productManagementDbContext.Incidents
            .FirstOrDefaultAsync(i => i.Id == request.IncidentId, cancellationToken);

        if (incident is null)
        {
            _logger.LogInformation("Incident {IncidentId} not found.", request.IncidentId);
            return Result.Failure("Incident not found.");
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return Result.Failure("A linked employee is required.");
        }

        var actor = EventActor.User(_currentUser.GetUserId(), employeeId.Value);
        var now = _dateTimeProvider.Now;

        var targetAlias = request.Action switch
        {
            IncidentWorkflowAction.BeginResponding => ProductStatusAlias.Responding,
            IncidentWorkflowAction.MarkRecovered => ProductStatusAlias.Resolved,
            IncidentWorkflowAction.Close => ProductStatusAlias.Closed,
            IncidentWorkflowAction.Cancel => ProductStatusAlias.Canceled,
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };

        // The status ids on the incident belong to the workflow it holds. The org assignment can name
        // another workflow with the same aliases and different ids, which makes the move illegal.
        var workflow = await _statusWorkflowDbContext.StatusWorkflows
            .Include(w => w.Statuses)
            .Include(w => w.Transitions)
                .ThenInclude(t => t.Conditions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == incident.StatusWorkflowId, cancellationToken);

        if (workflow is null)
        {
            _logger.LogError("Incident {IncidentId} has no status workflow {WorkflowId}.", incident.Id, incident.StatusWorkflowId);
            return Result.Failure("The incident's status workflow could not be loaded.");
        }

        var status = workflow.StatusFor((int)targetAlias);
        var target = status is null ? null : workflow.TargetRef(status.Id);
        if (target is null)
        {
            _logger.LogInformation(
                "Workflow {WorkflowId} has no status for {Alias}.", workflow.Id, targetAlias);
            return Result.Failure($"'{workflow.Name}' has no status for this action.");
        }

        var result = request.Action switch
        {
            IncidentWorkflowAction.BeginResponding => incident.BeginResponding(target, actor, now),
            IncidentWorkflowAction.MarkRecovered => incident.MarkRecovered(target, actor, now),
            IncidentWorkflowAction.Close => incident.Close(target, actor, now),
            IncidentWorkflowAction.Cancel => incident.Cancel(target, actor, now, request.Reason),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };

        if (result.IsFailure)
        {
            incident.ClearDomainEvents();
            return result;
        }

        await _productManagementDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
