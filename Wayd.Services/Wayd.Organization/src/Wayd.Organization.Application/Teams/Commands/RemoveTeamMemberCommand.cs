namespace Wayd.Organization.Application.Teams.Commands;

public sealed record RemoveTeamMemberCommand(Guid TeamId, Guid EmployeeId) : ICommand;

public sealed class RemoveTeamMemberCommandValidator : CustomValidator<RemoveTeamMemberCommand>
{
    public RemoveTeamMemberCommandValidator()
    {
        RuleFor(c => c.TeamId).NotEmpty();
        RuleFor(c => c.EmployeeId).NotEmpty();
    }
}

internal sealed class RemoveTeamMemberCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<RemoveTeamMemberCommandHandler> logger)
    : ICommandHandler<RemoveTeamMemberCommand>
{
    private const string AppRequestName = nameof(RemoveTeamMemberCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<RemoveTeamMemberCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveTeamMemberCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var team = await _organizationDbContext.BaseTeams
                .Include(t => t.Members)
                .SingleOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);

            if (team is null)
            {
                _logger.LogInformation("Team with Id {TeamId} not found.", request.TeamId);
                return Result.Failure("Team not found.");
            }

            var result = team.RemoveMember(request.EmployeeId);
            if (result.IsFailure)
            {
                _logger.LogError("Error removing member {EmployeeId} from team {TeamId}. Error: {Error}", request.EmployeeId, request.TeamId, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Employee {EmployeeId} removed from team {TeamId}.", request.EmployeeId, request.TeamId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
