using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Commands;

/// <summary>
/// Records that the team completed the sprint at <paramref name="CompletedAt"/>, or now when it is omitted.
/// </summary>
public sealed record CompleteSprintCommand(Guid Id, Instant? CompletedAt = null) : ICommand, IRequireLinkedEmployee;

public sealed class CompleteSprintCommandValidator : AbstractValidator<CompleteSprintCommand>
{
    public CompleteSprintCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();
    }
}

public sealed class CompleteSprintCommandHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<CompleteSprintCommandHandler> logger)
    : ICommandHandler<CompleteSprintCommand>
{
    private const string AppRequestName = nameof(CompleteSprintCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CompleteSprintCommandHandler> _logger = logger;

    public async Task<Result> Handle(CompleteSprintCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await SprintLifecycleChange.Apply(
                _workDbContext, _dispatcher, _schedulingSettings, _currentUser, _currentPrincipal, _dateTimeProvider, _logger,
                request.Id,
                "complete",
                (sprint, timeline, actor, now) => [() => sprint.Complete(timeline, request.CompletedAt ?? now, actor, now)],
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
