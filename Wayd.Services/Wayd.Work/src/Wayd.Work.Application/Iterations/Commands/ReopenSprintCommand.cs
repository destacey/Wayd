using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Commands;

/// <summary>
/// Clears a completed sprint's completion, while the team has not started a later sprint.
/// </summary>
public sealed record ReopenSprintCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class ReopenSprintCommandValidator : AbstractValidator<ReopenSprintCommand>
{
    public ReopenSprintCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();
    }
}

public sealed class ReopenSprintCommandHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReopenSprintCommandHandler> logger)
    : ICommandHandler<ReopenSprintCommand>
{
    private const string AppRequestName = nameof(ReopenSprintCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ReopenSprintCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReopenSprintCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await SprintLifecycleChange.Apply(
                _workDbContext, _dispatcher, _schedulingSettings, _currentUser, _currentPrincipal, _dateTimeProvider, _logger,
                request.Id,
                "reopen",
                (sprint, timeline, actor, now) => sprint.Reopen(timeline, actor, now),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
