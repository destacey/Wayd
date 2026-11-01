using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Records that an accepted intake request became a project (one started for it, or an existing one whose
/// scope absorbed it) or a work item. Needs an agent on the request's desk.
/// </summary>
/// <remarks>
/// A project is checked against Demand Management's copy of it, never PPM's tables; a project created moments
/// ago may not have reached the copy yet, which reads as not found. Demand Management keeps no copy of work
/// items, so for a work item the caller asks Work and hands in what it found as <see cref="WorkItem"/>.
/// Nothing about the target changes.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="Kind">What the request became.</param>
/// <param name="TargetId">The project or work item, by its id in its own module.</param>
/// <param name="Note">What the outcome covers; required for existing project scope.</param>
public sealed record AddIntakeRequestOutcomeCommand(Guid Id, IntakeRequestOutcomeKind Kind, Guid TargetId, string? Note)
    : ICommand, IRequireLinkedEmployee
{
    /// <summary>
    /// The work item <see cref="TargetId"/> names, as Work reported it, for a work item outcome; null when Work
    /// has no such work item. Ignored for the project kinds.
    /// </summary>
    public IntakeRequestOutcomeWorkItemDto? WorkItem { get; init; }
}

public sealed class AddIntakeRequestOutcomeCommandValidator : CustomValidator<AddIntakeRequestOutcomeCommand>
{
    public AddIntakeRequestOutcomeCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Kind).IsInEnum();
        RuleFor(c => c.TargetId).NotEmpty();
        RuleFor(c => c.Note).MaximumLength(1024);
    }
}

public sealed class AddIntakeRequestOutcomeCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AddIntakeRequestOutcomeCommand>
{
    /// <summary>The failure for a project Demand Management holds no copy of.</summary>
    public const string ProjectNotFoundError = "The project was not found.";

    /// <summary>The failure for a work item Work does not have.</summary>
    public const string WorkItemNotFoundError = "The work item was not found.";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(AddIntakeRequestOutcomeCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();
        var employeeId = reader.EmployeeId.Value;

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken,
            includeOutcomes: true);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAnAgentError);

        var targetCheck = await CheckTargetExists(request, cancellationToken);
        if (targetCheck.IsFailure)
            return targetCheck;

        var result = intakeRequest.AddOutcome(request.Kind, request.TargetId, request.Note, employeeId,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Whether the target exists: a work item by what Work reported, a project by Demand Management's copy.
    /// Runs after the agent check, so a caller who may not change the request learns nothing about the target.
    /// </summary>
    private async Task<Result> CheckTargetExists(AddIntakeRequestOutcomeCommand request, CancellationToken cancellationToken)
    {
        if (request.Kind == IntakeRequestOutcomeKind.WorkItem)
            return request.WorkItem?.Id == request.TargetId
                ? Result.Success()
                : Result.Failure(WorkItemNotFoundError);

        return await _dbContext.DemandProjects.AnyAsync(p => p.Id == request.TargetId, cancellationToken)
            ? Result.Success()
            : Result.Failure(ProjectNotFoundError);
    }
}
