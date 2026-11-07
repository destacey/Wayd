namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;

/// <summary>
/// Sets which plan tasks serve a deliverable, replacing its links. Linking is plan work: it needs the permission
/// to update projects and no delivery leadership, and it leaves the project's revision alone.
/// </summary>
/// <param name="ProjectId">The project the deliverable and tasks belong to.</param>
/// <param name="DeliverableId">The deliverable whose links are replaced.</param>
/// <param name="TaskIds">Every task that should serve the deliverable; empty unlinks them all.</param>
public sealed record SetProjectDeliverableTasksCommand(
    Guid ProjectId,
    Guid DeliverableId,
    IReadOnlyList<Guid> TaskIds) : ICommand;

/// <summary>Validates a <see cref="SetProjectDeliverableTasksCommand"/> before it runs.</summary>
public sealed class SetProjectDeliverableTasksCommandValidator : CustomValidator<SetProjectDeliverableTasksCommand>
{
    public SetProjectDeliverableTasksCommandValidator()
    {
        RuleFor(c => c.ProjectId).NotEmpty();
        RuleFor(c => c.DeliverableId).NotEmpty();
        RuleFor(c => c.TaskIds).NotNull();
        RuleForEach(c => c.TaskIds).NotEmpty();
    }
}

/// <summary>Replaces a deliverable's task links, loading only that deliverable and the named tasks.</summary>
public sealed class SetProjectDeliverableTasksCommandHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : ICommandHandler<SetProjectDeliverableTasksCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<Result> Handle(SetProjectDeliverableTasksCommand request, CancellationToken cancellationToken)
    {
        var taskIds = request.TaskIds.Distinct().ToList();

        // That deliverable with its links, and only the named tasks: never the whole plan.
        var project = await _ppmDbContext.Projects
            .AsSplitQuery()
            .Include(p => p.Deliverables.Where(d => d.Id == request.DeliverableId))
                .ThenInclude(d => d.TaskLinks)
            .Include(p => p.Tasks.Where(t => taskIds.Contains(t.Id)))
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (project is null)
            return Result.Failure(ProjectDeliverableCommandSupport.ProjectNotFoundError);

        if (project.Deliverables.All(d => d.Id != request.DeliverableId))
            return Result.Failure(ProjectDeliverableCommandSupport.DeliverableNotFoundError);

        var linked = project.SetDeliverableTasks(request.DeliverableId, taskIds);
        if (linked.IsFailure)
            return linked;

        try
        {
            await _ppmDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException)
        {
            // Links carry no concurrency token, so another request adding the same link first surfaces here as
            // a duplicate key; answering it as stale lets the caller reload and retry.
            return Result.Failure(ProjectSaves.StaleError);
        }
    }
}
