namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;

/// <summary>Puts a project's deliverables in a new order, naming every one of them exactly once.</summary>
/// <param name="ExpectedRevision">The project's revision when the change was prepared; a project changed since refuses it.</param>
public sealed record ReorderProjectDeliverablesCommand(
    Guid ProjectId,
    IReadOnlyList<Guid> DeliverableIds,
    int? ExpectedRevision) : ICommand, IRequireLinkedEmployee;

public sealed class ReorderProjectDeliverablesCommandValidator : CustomValidator<ReorderProjectDeliverablesCommand>
{
    public ReorderProjectDeliverablesCommandValidator()
    {
        RuleFor(c => c.ProjectId).NotEmpty();
        RuleFor(c => c.DeliverableIds).NotNull();
    }
}

public sealed class ReorderProjectDeliverablesCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ReorderProjectDeliverablesCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ReorderProjectDeliverablesCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

        // Every deliverable: the new order must name each of them.
        var project = await _ppmDbContext.Projects
            .WithLeadership()
            .Include(p => p.Deliverables)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (project is null)
            return Result.Failure(ProjectDeliverableCommandSupport.ProjectNotFoundError);

        if (!project.IsCurrent(request.ExpectedRevision))
            return Result.Failure(ProjectDeliverableCommandSupport.StaleError);

        var reordered = project.ReorderDeliverables(actor, project.AncestryRoles(), request.DeliverableIds, _dateTimeProvider.Now);
        if (reordered.IsFailure)
            return reordered;

        return await _ppmDbContext.SaveDeliverableChange(cancellationToken);
    }
}
