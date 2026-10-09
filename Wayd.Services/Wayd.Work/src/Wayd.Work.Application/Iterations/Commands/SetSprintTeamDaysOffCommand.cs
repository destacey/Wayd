using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Commands;

/// <summary>
/// Replaces the days within the sprint that the whole team is off, such as an offsite. Allowed for the same
/// people as the sprint's lifecycle: members of its team or team of teams, or an iterations administrator.
/// </summary>
public sealed record SetSprintTeamDaysOffCommand(Guid Id, IReadOnlyList<LocalDate> TeamDaysOff) : ICommand, IRequireLinkedEmployee;

public sealed class SetSprintTeamDaysOffCommandValidator : AbstractValidator<SetSprintTeamDaysOffCommand>
{
    public SetSprintTeamDaysOffCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.TeamDaysOff)
            .NotNull();
    }
}

public sealed class SetSprintTeamDaysOffCommandHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<SetSprintTeamDaysOffCommandHandler> logger)
    : ICommandHandler<SetSprintTeamDaysOffCommand>
{
    private const string AppRequestName = nameof(SetSprintTeamDaysOffCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SetSprintTeamDaysOffCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetSprintTeamDaysOffCommand request, CancellationToken cancellationToken)
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
                return Result.Failure("Only members of the sprint's team, or of its team of teams, can change its team days off.");

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            var result = sprint.SetTeamDaysOff(request.TeamDaysOff, EventActor.User(_currentUser.GetUserId(), employeeId), now);
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
