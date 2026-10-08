using Wayd.Common.Application.SystemSettings.Scheduling;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Commands;

public sealed record UpdateTeamOperatingModelCommand(
    Guid TeamId,
    Guid OperatingModelId,
    Methodology Methodology,
    SizingMethod SizingMethod,
    string TimeZone,
    int CommitmentGraceDays,
    IReadOnlyList<IsoDayOfWeek> WorkingDays,
    Guid? HolidayCalendarId) : ICommand;

public sealed class UpdateTeamOperatingModelCommandValidator : CustomValidator<UpdateTeamOperatingModelCommand>
{
    public UpdateTeamOperatingModelCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.TeamId)
            .NotEmpty();

        RuleFor(c => c.OperatingModelId)
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
            .IsWorkingWeek();
    }
}

public sealed class UpdateTeamOperatingModelCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<UpdateTeamOperatingModelCommandHandler> logger) : ICommandHandler<UpdateTeamOperatingModelCommand>
{
    private const string RequestName = nameof(UpdateTeamOperatingModelCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateTeamOperatingModelCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateTeamOperatingModelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Load through Team aggregate
            var team = await _organizationDbContext.Teams
                .Include(t => t.OperatingModels)
                .FirstOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);

            if (team is null)
            {
                _logger.LogInformation("Team {TeamId} not found", request.TeamId);
                return Result.Failure($"Team with Id {request.TeamId} not found.");
            }

            if (!team.OperatingModels.Any(m => m.Id == request.OperatingModelId))
            {
                _logger.LogInformation("Operating model {OperatingModelId} for Team {TeamId} not found",
                    request.OperatingModelId, request.TeamId);
                return Result.Failure($"Operating model with Id {request.OperatingModelId} for Team {request.TeamId} not found.");
            }

            var workingWeek = WorkingWeek.Create(request.WorkingDays);
            if (workingWeek.IsFailure)
                return Result.Failure(workingWeek.Error);

            if (request.HolidayCalendarId is { } calendarId
                && !await _organizationDbContext.HolidayCalendars.AnyAsync(c => c.Id == calendarId, cancellationToken))
                return Result.Failure($"Holiday calendar {calendarId} not found.");

            var updateResult = team.CorrectOperatingModel(
                request.OperatingModelId,
                request.Methodology,
                request.SizingMethod,
                request.TimeZone,
                request.CommitmentGraceDays,
                workingWeek.Value,
                request.HolidayCalendarId,
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                _dateTimeProvider.Now);
            if (updateResult.IsFailure)
            {
                _logger.LogError("Failed to update operating model {OperatingModelId}. Error: {Error}",
                    request.OperatingModelId, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated TeamOperatingModel {OperatingModelId} for Team {TeamId}",
                request.OperatingModelId, request.TeamId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}: {@Request}", RequestName, request);
            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }
}
