namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;

/// <summary>Replaces a deliverable's name, description, acceptance criteria, owner and target date.</summary>
/// <param name="ExpectedRevision">The project's revision when the change was prepared; a project changed since refuses it.</param>
public sealed record UpdateProjectDeliverableCommand(
    Guid ProjectId,
    Guid DeliverableId,
    string Name,
    string? Description,
    string? AcceptanceCriteria,
    Guid? OwnerId,
    LocalDate? TargetDate,
    int? ExpectedRevision) : ICommand, IRequireLinkedEmployee;

public sealed class UpdateProjectDeliverableCommandValidator : CustomValidator<UpdateProjectDeliverableCommand>
{
    public UpdateProjectDeliverableCommandValidator()
    {
        RuleFor(c => c.ProjectId).NotEmpty();
        RuleFor(c => c.DeliverableId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Description).MaximumLength(4096);
        RuleFor(c => c.AcceptanceCriteria).MaximumLength(4096);
        RuleFor(c => c.OwnerId).NotEqual(Guid.Empty);
    }
}

public sealed class UpdateProjectDeliverableCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateProjectDeliverableCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateProjectDeliverableCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

        var project = await _ppmDbContext.Projects
            .WithLeadership()
            .Include(p => p.Deliverables.Where(d => d.Id == request.DeliverableId))
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (project is null)
            return Result.Failure(ProjectDeliverableCommandSupport.ProjectNotFoundError);

        if (!project.IsCurrent(request.ExpectedRevision))
            return Result.Failure(ProjectDeliverableCommandSupport.StaleError);

        var currentOwnerId = project.Deliverables.FirstOrDefault()?.OwnerId;
        var owner = await _ppmDbContext.CheckOwner(request.OwnerId, currentOwnerId, cancellationToken);
        if (owner.IsFailure)
            return owner;

        var updated = project.UpdateDeliverable(actor, project.AncestryRoles(), request.DeliverableId, request.Name,
            request.Description, request.AcceptanceCriteria, request.OwnerId, request.TargetDate, _dateTimeProvider.Now);
        if (updated.IsFailure)
            return Result.Failure(updated.Error);

        return await _ppmDbContext.SaveDeliverableChange(cancellationToken);
    }
}
