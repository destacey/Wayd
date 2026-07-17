using Wayd.Organization.Domain.Enums;

namespace Wayd.Organization.Application.Teams.Commands;

public sealed record UpdateTeamOperatingModelCommand(
    Guid TeamId,
    Guid OperatingModelId,
    Methodology Methodology,
    SizingMethod SizingMethod) : ICommand;

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
    }
}

public sealed class UpdateTeamOperatingModelCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<UpdateTeamOperatingModelCommandHandler> logger) : ICommandHandler<UpdateTeamOperatingModelCommand>
{
    private const string RequestName = nameof(UpdateTeamOperatingModelCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
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

            var operatingModel = team.OperatingModels.SingleOrDefault(m => m.Id == request.OperatingModelId);
            if (operatingModel is null)
            {
                _logger.LogInformation("Operating model {OperatingModelId} for Team {TeamId} not found",
                    request.OperatingModelId, request.TeamId);
                return Result.Failure($"Operating model with Id {request.OperatingModelId} for Team {request.TeamId} not found.");
            }

            var updateResult = operatingModel.Update(request.Methodology, request.SizingMethod);
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
