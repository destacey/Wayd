using Wayd.Common.Application.SystemSettings.Scheduling;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Commands;

/// <summary>
/// Sets a new operating model for a team from <paramref name="StartDate"/>. <paramref name="WorkingDays"/> left
/// null carries over the current model's working week, or Monday to Friday for a team with none.
/// </summary>
public sealed record SetTeamOperatingModelCommand(
    Guid TeamId,
    LocalDate StartDate,
    Methodology Methodology,
    SizingMethod SizingMethod,
    string TimeZone,
    int CommitmentGraceDays,
    IReadOnlyList<IsoDayOfWeek>? WorkingDays,
    Guid? HolidayCalendarId) : ICommand<Guid>;

public sealed class SetTeamOperatingModelCommandValidator : CustomValidator<SetTeamOperatingModelCommand>
{
    public SetTeamOperatingModelCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.TeamId)
            .NotEmpty();

        RuleFor(c => c.StartDate)
            .NotEmpty();

        RuleFor(c => c.Methodology)
            .IsInEnum();

        RuleFor(c => c.SizingMethod)
            .IsInEnum();

        RuleFor(c => c.TimeZone)
            .NotEmpty()
            .IsIanaTimeZone();

        RuleFor(c => c.CommitmentGraceDays)
            .InclusiveBetween(0, SchedulingSettingsValidator.MaxCommitmentGraceDays);

        RuleFor(c => c.WorkingDays)
            .IsWorkingWeek()
            .When(c => c.WorkingDays is not null);
    }
}

public sealed class SetTeamOperatingModelCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<SetTeamOperatingModelCommandHandler> logger) : ICommandHandler<SetTeamOperatingModelCommand, Guid>
{
    private const string RequestName = nameof(SetTeamOperatingModelCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<SetTeamOperatingModelCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(SetTeamOperatingModelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var team = await _organizationDbContext.Teams
                .Include(t => t.OperatingModels.Where(m => m.DateRange.End == null))
                .FirstOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);
            if (team is null)
            {
                _logger.LogInformation("Team {TeamId} not found", request.TeamId);
                return Result.Failure<Guid>($"Team with Id {request.TeamId} not found.");
            }

            var workingWeek = request.WorkingDays is null
                ? Result.Success(team.OperatingModels.SingleOrDefault(m => m.IsCurrent)?.WorkingWeek ?? WorkingWeek.MondayToFriday)
                : WorkingWeek.Create(request.WorkingDays);
            if (workingWeek.IsFailure)
                return Result.Failure<Guid>(workingWeek.Error);

            if (request.HolidayCalendarId is { } calendarId
                && !await _organizationDbContext.HolidayCalendars.AnyAsync(c => c.Id == calendarId, cancellationToken))
                return Result.Failure<Guid>($"Holiday calendar {calendarId} not found.");

            var result = team.SetOperatingModel(request.StartDate, request.Methodology, request.SizingMethod, request.TimeZone, request.CommitmentGraceDays, workingWeek.Value, request.HolidayCalendarId, EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to set operating model for Team {TeamId}. Error: {Error}",
                    request.TeamId, result.Error);
                return Result.Failure<Guid>(result.Error);
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Created TeamOperatingModel {OperatingModelId} for Team {TeamId}",
                result.Value.Id, request.TeamId);

            return Result.Success(result.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}: {@Request}", RequestName, request);
            return Result.Failure<Guid>($"Exception for request {RequestName}: {ex.Message}");
        }
    }
}
