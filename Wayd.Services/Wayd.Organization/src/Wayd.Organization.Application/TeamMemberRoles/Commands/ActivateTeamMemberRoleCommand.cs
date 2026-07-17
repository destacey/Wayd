namespace Wayd.Organization.Application.TeamMemberRoles.Commands;

public sealed record ActivateTeamMemberRoleCommand(Guid Id) : ICommand;

public sealed class ActivateTeamMemberRoleCommandValidator : CustomValidator<ActivateTeamMemberRoleCommand>
{
    public ActivateTeamMemberRoleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class ActivateTeamMemberRoleCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<ActivateTeamMemberRoleCommandHandler> logger)
    : ICommandHandler<ActivateTeamMemberRoleCommand>
{
    private const string AppRequestName = nameof(ActivateTeamMemberRoleCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<ActivateTeamMemberRoleCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateTeamMemberRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await _organizationDbContext.TeamMemberRoles
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (role is null)
            {
                _logger.LogInformation("Team member role with Id {TeamMemberRoleId} not found.", request.Id);
                return Result.Failure("Team member role not found.");
            }

            var result = role.Activate();
            if (result.IsFailure)
            {
                _logger.LogError("Error activating team member role {TeamMemberRoleId}. Error message: {Error}", request.Id, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Team member role {TeamMemberRoleId} activated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
