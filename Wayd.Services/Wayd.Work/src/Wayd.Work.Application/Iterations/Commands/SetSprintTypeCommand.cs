using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Commands;

/// <summary>
/// Sets the sprint's type, holding it whatever its planning interval mapping says, or clears it with a null
/// <see cref="SprintType"/> so the sprint follows its mapping again. Allowed for the same people as the
/// sprint's lifecycle: members of its team or team of teams, or an iterations administrator.
/// </summary>
public sealed record SetSprintTypeCommand(Guid Id, SprintType? SprintType) : ICommand, IRequireLinkedEmployee;

public sealed class SetSprintTypeCommandValidator : AbstractValidator<SetSprintTypeCommand>
{
    public SetSprintTypeCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.SprintType)
            .IsInEnum();
    }
}

public sealed class SetSprintTypeCommandHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<SetSprintTypeCommandHandler> logger)
    : ICommandHandler<SetSprintTypeCommand>
{
    private const string AppRequestName = nameof(SetSprintTypeCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SetSprintTypeCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetSprintTypeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var sprint = await _workDbContext.Iterations
                .FirstOrDefaultAsync(i => i.Id == request.Id && i.Type == IterationType.Sprint, cancellationToken);
            if (sprint is null)
                return Result.Failure("Sprint not found.");

            if (sprint.TeamId is not { } teamId)
                return Result.Failure("The sprint's team is not mapped to a Wayd team.");

            var now = _dateTimeProvider.Now;
            if (!await _currentPrincipal.CanManageTeamSprints(_dispatcher, teamId, now.InUtc().Date, cancellationToken))
                return Result.Failure("Only members of the sprint's team, or of its team of teams, can change its sprint type.");

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
            var result = request.SprintType is { } sprintType
                ? sprint.SetSprintType(sprintType, actor, now)
                : sprint.ClearSprintType(actor, now);
            if (result.IsFailure)
                return result;

            await _workDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
