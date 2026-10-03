using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Commands;

/// <summary>
/// Records that the team started the sprint at <paramref name="StartedAt"/>, or now when it is omitted. When
/// another of the team's sprints is still open, <paramref name="CompleteOpenSprint"/> confirms completing it at
/// the same moment.
/// </summary>
public sealed record StartSprintCommand(Guid Id, bool CompleteOpenSprint, Instant? StartedAt = null) : ICommand, IRequireLinkedEmployee;

public sealed class StartSprintCommandValidator : AbstractValidator<StartSprintCommand>
{
    public StartSprintCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();
    }
}

public sealed class StartSprintCommandHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<StartSprintCommandHandler> logger)
    : ICommandHandler<StartSprintCommand>
{
    private const string AppRequestName = nameof(StartSprintCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<StartSprintCommandHandler> _logger = logger;

    public async Task<Result> Handle(StartSprintCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await SprintLifecycleChange.Apply(
                _workDbContext, _dispatcher, _schedulingSettings, _currentUser, _currentPrincipal, _dateTimeProvider, _logger,
                request.Id,
                "start",
                (sprint, timeline, actor, now) => Steps(request, sprint, timeline, actor, now),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }

    private static IReadOnlyList<Func<Result>> Steps(StartSprintCommand request, Iteration sprint, TeamSprintTimeline timeline, EventActor actor, Instant now)
    {
        var startedAt = request.StartedAt ?? now;

        if (timeline.OpenSprint is not { } open || open == sprint)
            return [() => sprint.Start(timeline, startedAt, actor, now)];

        if (!request.CompleteOpenSprint)
            return [() => Result.Failure($"{open.Name} is still open. Confirm completing it to start this sprint.")];

        // The open sprint is completed, and saved, before this one starts: SQL Server checks the open-sprint
        // index after each statement, so a start that reached it first would leave the team two open sprints
        // and fail the save. The start is checked first so a refused one completes nothing.
        return
        [
            () =>
            {
                var allowed = timeline.CanStart(sprint, startedAt, now);
                return allowed.IsFailure ? allowed : open.Complete(timeline, startedAt, actor, now);
            },
            () => sprint.Start(timeline, startedAt, actor, now),
        ];
    }
}
