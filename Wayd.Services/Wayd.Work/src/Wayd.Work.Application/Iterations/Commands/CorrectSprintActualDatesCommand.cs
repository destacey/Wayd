using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Commands;

/// <summary>
/// Corrects the actual start and completion of one or more of a team's sprints, after the fact. Each entry
/// replaces both values; a null value reverts to the sprint's default. Sprints corrected together are checked
/// against where each other ends up, so moving one sprint's completion past the next one's start is accepted
/// only alongside a matching correction of the next one.
/// </summary>
public sealed record CorrectSprintActualDatesCommand(IReadOnlyList<SprintActualDatesCorrection> Sprints) : ICommand, IRequireLinkedEmployee;

public sealed record SprintActualDatesCorrection(Guid SprintId, Instant? Started, Instant? Completed);

public sealed class CorrectSprintActualDatesCommandValidator : AbstractValidator<CorrectSprintActualDatesCommand>
{
    public CorrectSprintActualDatesCommandValidator()
    {
        RuleFor(c => c.Sprints)
            .NotEmpty()
            .Must(s => s.Select(c => c.SprintId).Distinct().Count() == s.Count)
                .WithMessage("Each sprint can be corrected only once.");

        RuleForEach(c => c.Sprints)
            .ChildRules(sprint =>
            {
                sprint.RuleFor(s => s.SprintId)
                    .NotEmpty();
            });
    }
}

public sealed class CorrectSprintActualDatesCommandHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<CorrectSprintActualDatesCommandHandler> logger)
    : ICommandHandler<CorrectSprintActualDatesCommand>
{
    private const string AppRequestName = nameof(CorrectSprintActualDatesCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CorrectSprintActualDatesCommandHandler> _logger = logger;

    public async Task<Result> Handle(CorrectSprintActualDatesCommand request, CancellationToken cancellationToken)
    {
        if (request.Sprints.Count == 0)
            return Result.Failure("At least one sprint must be corrected.");

        try
        {
            return await SprintLifecycleChange.Apply(
                _workDbContext, _dispatcher, _schedulingSettings, _currentUser, _currentPrincipal, _dateTimeProvider, _logger,
                request.Sprints[0].SprintId,
                "correct the actual dates of",
                (_, timeline, actor, now) => Steps(request, timeline, actor, now),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }

    private static IReadOnlyList<Func<Result>> Steps(CorrectSprintActualDatesCommand request, TeamSprintTimeline timeline, EventActor actor, Instant now)
    {
        var sprintsById = timeline.Sprints.ToDictionary(s => s.Id);
        if (request.Sprints.Any(c => !sprintsById.ContainsKey(c.SprintId)))
            return [() => Result.Failure("Only sprints of one team, with planned dates, can be corrected together.")];

        var correction = timeline.ValidateCorrection(
            request.Sprints.ToDictionary(c => sprintsById[c.SprintId], c => new SprintActualDates(c.Started, c.Completed)),
            now);
        if (correction.IsFailure)
            return [() => Result.Failure(correction.Error)];

        // The sprint left open is saved last: SQL Server checks the open-sprint index after each statement, so
        // opening it before the team's current open sprint is closed would fail the save.
        return [.. correction.Value.Sprints
            .OrderBy(c => c.Value is { Started: not null, Completed: null })
            .Select(c => (Func<Result>)(() =>
            {
                c.Key.CorrectActualDates(correction.Value, actor, now);
                return Result.Success();
            }))];
    }
}
