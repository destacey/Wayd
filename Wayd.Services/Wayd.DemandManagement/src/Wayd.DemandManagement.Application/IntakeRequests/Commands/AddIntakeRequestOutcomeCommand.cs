using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Records that an accepted intake request became a project: one started for it, or an existing one whose
/// scope absorbed it. Needs an agent on the request's desk.
/// </summary>
/// <remarks>
/// The project is checked against Demand Management's copy of it, never PPM's tables, and nothing about the
/// project changes. A project created moments ago may not have reached the copy yet, which reads as not found.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="Kind">What the request became.</param>
/// <param name="ProjectId">The project.</param>
/// <param name="Note">What the outcome covers; required for existing project scope.</param>
public sealed record AddIntakeRequestOutcomeCommand(Guid Id, IntakeRequestOutcomeKind Kind, Guid ProjectId, string? Note)
    : ICommand, IRequireLinkedEmployee;

public sealed class AddIntakeRequestOutcomeCommandValidator : CustomValidator<AddIntakeRequestOutcomeCommand>
{
    public AddIntakeRequestOutcomeCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Kind).IsInEnum();
        RuleFor(c => c.ProjectId).NotEmpty();
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

        if (!await _dbContext.DemandProjects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken))
            return Result.Failure(ProjectNotFoundError);

        var result = intakeRequest.AddOutcome(request.Kind, request.ProjectId, request.Note, employeeId,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
