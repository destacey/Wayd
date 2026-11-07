namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;

/// <summary>
/// Deletes a deliverable not yet started, from a project not yet started. Later, a deliverable is descoped instead.
/// </summary>
/// <param name="ExpectedRevision">The project's revision when the change was prepared; a project changed since refuses it.</param>
public sealed record DeleteProjectDeliverableCommand(
    Guid ProjectId,
    Guid DeliverableId,
    int? ExpectedRevision) : ICommand, IRequireLinkedEmployee;

public sealed class DeleteProjectDeliverableCommandValidator : CustomValidator<DeleteProjectDeliverableCommand>
{
    public DeleteProjectDeliverableCommandValidator()
    {
        RuleFor(c => c.ProjectId).NotEmpty();
        RuleFor(c => c.DeliverableId).NotEmpty();
    }
}

public sealed class DeleteProjectDeliverableCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteProjectDeliverableCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteProjectDeliverableCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

        var project = await _ppmDbContext.Projects
            .WithLeadership()
            .Include(p => p.Deliverables.Where(d => d.Id == request.DeliverableId))
                // The database does not cascade a deliverable's links; unloaded, they fail the save.
                .ThenInclude(d => d.TaskLinks)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (project is null)
            return Result.Failure(ProjectDeliverableCommandSupport.ProjectNotFoundError);

        if (!project.IsCurrent(request.ExpectedRevision))
            return Result.Failure(ProjectSaves.StaleError);

        if (project.Deliverables.All(d => d.Id != request.DeliverableId))
            return Result.Failure(ProjectDeliverableCommandSupport.DeliverableNotFoundError);

        var removed = project.RemoveDeliverable(actor, project.AncestryRoles(), request.DeliverableId, _dateTimeProvider.Now);
        if (removed.IsFailure)
            return removed;

        return await _ppmDbContext.SaveProjectChange(cancellationToken);
    }
}
