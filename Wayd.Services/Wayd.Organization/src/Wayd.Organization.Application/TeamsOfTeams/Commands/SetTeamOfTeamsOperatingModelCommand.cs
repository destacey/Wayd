using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Organization.Application.TeamsOfTeams.Commands;

public sealed record SetTeamOfTeamsOperatingModelCommand(Guid TeamId, LocalDate StartDate, string TimeZone) : ICommand<Guid>;

public sealed class SetTeamOfTeamsOperatingModelCommandValidator : CustomValidator<SetTeamOfTeamsOperatingModelCommand>
{
    public SetTeamOfTeamsOperatingModelCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.TeamId)
            .NotEmpty();

        RuleFor(c => c.StartDate)
            .NotEmpty();

        RuleFor(c => c.TimeZone)
            .NotEmpty()
            .IsIanaTimeZone();
    }
}

public sealed class SetTeamOfTeamsOperatingModelCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<SetTeamOfTeamsOperatingModelCommandHandler> logger) : ICommandHandler<SetTeamOfTeamsOperatingModelCommand, Guid>
{
    private const string RequestName = nameof(SetTeamOfTeamsOperatingModelCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<SetTeamOfTeamsOperatingModelCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(SetTeamOfTeamsOperatingModelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var team = await _organizationDbContext.TeamOfTeams
                .Include(t => t.OperatingModels.Where(m => m.DateRange.End == null))
                .FirstOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);
            if (team is null)
            {
                _logger.LogInformation("Team of Teams {TeamId} not found", request.TeamId);
                return Result.Failure<Guid>($"Team of Teams with Id {request.TeamId} not found.");
            }

            var result = team.SetOperatingModel(request.StartDate, request.TimeZone, EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to set operating model for Team of Teams {TeamId}. Error: {Error}",
                    request.TeamId, result.Error);
                return Result.Failure<Guid>(result.Error);
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Created TeamOfTeamsOperatingModel {OperatingModelId} for Team of Teams {TeamId}",
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
