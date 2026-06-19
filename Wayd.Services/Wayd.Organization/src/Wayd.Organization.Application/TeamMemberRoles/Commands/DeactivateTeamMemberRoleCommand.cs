namespace Wayd.Organization.Application.TeamMemberRoles.Commands;

public sealed record DeactivateTeamMemberRoleCommand(Guid Id) : ICommand;

public sealed class DeactivateTeamMemberRoleCommandValidator : CustomValidator<DeactivateTeamMemberRoleCommand>
{
    public DeactivateTeamMemberRoleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

internal sealed class DeactivateTeamMemberRoleCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<DeactivateTeamMemberRoleCommandHandler> logger)
    : ICommandHandler<DeactivateTeamMemberRoleCommand>
{
    private const string AppRequestName = nameof(DeactivateTeamMemberRoleCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<DeactivateTeamMemberRoleCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeactivateTeamMemberRoleCommand request, CancellationToken cancellationToken)
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

            var result = role.Deactivate();
            if (result.IsFailure)
            {
                _logger.LogError("Error deactivating team member role {TeamMemberRoleId}. Error message: {Error}", request.Id, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Team member role {TeamMemberRoleId} deactivated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
