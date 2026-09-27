using Wayd.Common.Domain.Events;

namespace Wayd.Organization.Application.TeamsOfTeams.Commands;

public sealed record UpdateTeamOfTeamsOperatingModelCommand(Guid TeamId, Guid OperatingModelId, string TimeZone) : ICommand;

public sealed class UpdateTeamOfTeamsOperatingModelCommandValidator : CustomValidator<UpdateTeamOfTeamsOperatingModelCommand>
{
    public UpdateTeamOfTeamsOperatingModelCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.TeamId)
            .NotEmpty();

        RuleFor(c => c.OperatingModelId)
            .NotEmpty();

        RuleFor(c => c.TimeZone)
            .NotEmpty()
            .IsIanaTimeZone();
    }
}

public sealed class UpdateTeamOfTeamsOperatingModelCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<UpdateTeamOfTeamsOperatingModelCommandHandler> logger) : ICommandHandler<UpdateTeamOfTeamsOperatingModelCommand>
{
    private const string RequestName = nameof(UpdateTeamOfTeamsOperatingModelCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateTeamOfTeamsOperatingModelCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateTeamOfTeamsOperatingModelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var team = await _organizationDbContext.TeamOfTeams
                .Include(t => t.OperatingModels)
                .FirstOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);
            if (team is null)
            {
                _logger.LogInformation("Team of Teams {TeamId} not found", request.TeamId);
                return Result.Failure($"Team of Teams with Id {request.TeamId} not found.");
            }

            var result = team.CorrectOperatingModel(
                request.OperatingModelId,
                request.TimeZone,
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to update operating model {OperatingModelId}. Error: {Error}",
                    request.OperatingModelId, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated TeamOfTeamsOperatingModel {OperatingModelId} for Team of Teams {TeamId}",
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
