using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Commands;

/// <summary>
/// Records that the team started the sprint now. When another of the team's sprints is still open,
/// <paramref name="CompleteOpenSprint"/> confirms completing it at the same instant.
/// </summary>
public sealed record StartSprintCommand(Guid Id, bool CompleteOpenSprint) : ICommand, IRequireLinkedEmployee;

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
                (sprint, timeline, actor, now) => sprint.Start(timeline, request.CompleteOpenSprint, actor, now),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
