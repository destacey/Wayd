using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;

/// <summary>Adds a deliverable to a project, last in its order and Not Started.</summary>
/// <param name="ExpectedRevision">The project's revision when the change was prepared; a project changed since refuses it.</param>
public sealed record CreateProjectDeliverableCommand(
    Guid ProjectId,
    string Name,
    string? Description,
    string? AcceptanceCriteria,
    Guid? OwnerId,
    LocalDate? TargetDate,
    int? ExpectedRevision) : ICommand<Guid>, IRequireLinkedEmployee;

public sealed class CreateProjectDeliverableCommandValidator : CustomValidator<CreateProjectDeliverableCommand>
{
    public CreateProjectDeliverableCommandValidator()
    {
        RuleFor(c => c.ProjectId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Description).MaximumLength(4096);
        RuleFor(c => c.AcceptanceCriteria).MaximumLength(4096);
        RuleFor(c => c.OwnerId).NotEqual(Guid.Empty);
    }
}

public sealed class CreateProjectDeliverableCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IStatusResolver statusResolver,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateProjectDeliverableCommand, Guid>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(CreateProjectDeliverableCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

        // Every deliverable, to place the new one last.
        var project = await _ppmDbContext.Projects
            .WithLeadership()
            .Include(p => p.Deliverables)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (project is null)
            return Result.Failure<Guid>(ProjectDeliverableCommandSupport.ProjectNotFoundError);

        if (!project.IsCurrent(request.ExpectedRevision))
            return Result.Failure<Guid>(ProjectDeliverableCommandSupport.StaleError);

        var owner = await _ppmDbContext.CheckOwner(request.OwnerId, currentOwnerId: null, cancellationToken);
        if (owner.IsFailure)
            return Result.Failure<Guid>(owner.Error);

        var initial = await _statusResolver.ForAlias(PpmWorkflowOwners.ProjectDeliverable.Key, scopeId: null,
            (int)ProjectDeliverableStatusAlias.NotStarted, cancellationToken);
        if (initial.IsFailure)
            return Result.Failure<Guid>(initial.Error);

        var added = project.AddDeliverable(actor, project.AncestryRoles(), request.Name, request.Description,
            request.AcceptanceCriteria, request.OwnerId, request.TargetDate, initial.Value, _dateTimeProvider.Now);
        if (added.IsFailure)
            return Result.Failure<Guid>(added.Error);

        var saved = await _ppmDbContext.SaveDeliverableChange(cancellationToken);
        return saved.IsSuccess
            ? Result.Success(added.Value.Id)
            : Result.Failure<Guid>(saved.Error);
    }
}
