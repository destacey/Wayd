using Wayd.Organization.Domain.Enums;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Commands;

public sealed record SetTeamOperatingModelCommand(
    Guid TeamId,
    LocalDate StartDate,
    Methodology Methodology,
    SizingMethod SizingMethod) : ICommand<Guid>;

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
    }
}

public sealed class SetTeamOperatingModelCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<SetTeamOperatingModelCommandHandler> logger) : ICommandHandler<SetTeamOperatingModelCommand, Guid>
{
    private const string RequestName = nameof(SetTeamOperatingModelCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
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

            var result = team.SetOperatingModel(request.StartDate, request.Methodology, request.SizingMethod);
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
