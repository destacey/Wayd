using Wayd.Common.Application.StatusWorkflows;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;

/// <summary>
/// Moves a deliverable to another status of the deliverable workflow: starting, accepting, descoping or
/// reopening it.
/// </summary>
/// <param name="Reason">Why; required for a Removed status such as Descoped.</param>
/// <param name="ExpectedRevision">The project's revision when the change was prepared; a project changed since refuses it.</param>
public sealed record ChangeProjectDeliverableStatusCommand(
    Guid ProjectId,
    Guid DeliverableId,
    Guid StatusId,
    string? Reason,
    int? ExpectedRevision) : ICommand, IRequireLinkedEmployee;

public sealed class ChangeProjectDeliverableStatusCommandValidator : CustomValidator<ChangeProjectDeliverableStatusCommand>
{
    public ChangeProjectDeliverableStatusCommandValidator()
    {
        RuleFor(c => c.ProjectId).NotEmpty();
        RuleFor(c => c.DeliverableId).NotEmpty();
        RuleFor(c => c.StatusId).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(1024);
    }
}

public sealed class ChangeProjectDeliverableStatusCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IStatusResolver statusResolver,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeProjectDeliverableStatusCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ChangeProjectDeliverableStatusCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

        var project = await _ppmDbContext.Projects
            .WithLeadership()
            .Include(p => p.Deliverables.Where(d => d.Id == request.DeliverableId))
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (project is null)
            return Result.Failure(ProjectDeliverableCommandSupport.ProjectNotFoundError);

        if (!project.IsCurrent(request.ExpectedRevision))
            return Result.Failure(ProjectSaves.StaleError);

        if (project.Deliverables.All(d => d.Id != request.DeliverableId))
            return Result.Failure(ProjectDeliverableCommandSupport.DeliverableNotFoundError);

        // Resolved through the governing workflow rather than by loading the status: that is what stops a
        // deliverable being moved to a status of some other workflow.
        var workflow = await _statusResolver.DeliverableWorkflow(cancellationToken);
        if (workflow.IsFailure)
            return Result.Failure(workflow.Error);

        var target = workflow.Value.TargetRef(request.StatusId);
        if (target is null)
            return Result.Failure($"That status does not belong to '{workflow.Value.Name}'.");

        var changed = project.ChangeDeliverableStatus(actor, project.AncestryRoles(), request.DeliverableId, target,
            request.Reason, _dateTimeProvider.Now);
        if (changed.IsFailure)
            return Result.Failure(changed.Error);

        return await _ppmDbContext.SaveProjectChange(cancellationToken);
    }
}
