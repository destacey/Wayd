namespace Wayd.Organization.Application.Teams.Commands;

public sealed record DeleteTeamOperatingModelCommand(Guid TeamId, Guid OperatingModelId) : ICommand;

public sealed class DeleteTeamOperatingModelCommandValidator : CustomValidator<DeleteTeamOperatingModelCommand>
{
    public DeleteTeamOperatingModelCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.TeamId)
            .NotEmpty();

        RuleFor(c => c.OperatingModelId)
            .NotEmpty();
    }
}

public sealed class DeleteTeamOperatingModelCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<DeleteTeamOperatingModelCommandHandler> logger) : ICommandHandler<DeleteTeamOperatingModelCommand>
{
    private const string RequestName = nameof(DeleteTeamOperatingModelCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<DeleteTeamOperatingModelCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteTeamOperatingModelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var team = await _organizationDbContext.Teams
                .Include(t => t.OperatingModels)
                .FirstOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);
            if (team is null)
            {
                _logger.LogInformation("Team {TeamId} not found", request.TeamId);
                return Result.Failure($"Team with Id {request.TeamId} not found.");
            }

            var result = team.RemoveOperatingModel(request.OperatingModelId);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to remove operating model {OperatingModelId} from Team {TeamId}. Error: {Error}",
                    request.OperatingModelId, request.TeamId, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Removed TeamOperatingModel {OperatingModelId} from Team {TeamId}",
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
