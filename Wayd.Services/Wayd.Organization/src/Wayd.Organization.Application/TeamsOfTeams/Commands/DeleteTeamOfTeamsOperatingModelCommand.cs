using Wayd.Common.Domain.Events;

namespace Wayd.Organization.Application.TeamsOfTeams.Commands;

public sealed record DeleteTeamOfTeamsOperatingModelCommand(Guid TeamId, Guid OperatingModelId) : ICommand;

public sealed class DeleteTeamOfTeamsOperatingModelCommandValidator : CustomValidator<DeleteTeamOfTeamsOperatingModelCommand>
{
    public DeleteTeamOfTeamsOperatingModelCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.TeamId)
            .NotEmpty();

        RuleFor(c => c.OperatingModelId)
            .NotEmpty();
    }
}

public sealed class DeleteTeamOfTeamsOperatingModelCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<DeleteTeamOfTeamsOperatingModelCommandHandler> logger) : ICommandHandler<DeleteTeamOfTeamsOperatingModelCommand>
{
    private const string RequestName = nameof(DeleteTeamOfTeamsOperatingModelCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<DeleteTeamOfTeamsOperatingModelCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteTeamOfTeamsOperatingModelCommand request, CancellationToken cancellationToken)
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

            var result = team.RemoveOperatingModel(request.OperatingModelId, EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to remove operating model {OperatingModelId} from Team of Teams {TeamId}. Error: {Error}",
                    request.OperatingModelId, request.TeamId, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Removed TeamOfTeamsOperatingModel {OperatingModelId} from Team of Teams {TeamId}",
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
