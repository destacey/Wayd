namespace Wayd.Organization.Application.TeamMemberRoles.Commands;

public sealed record DeleteTeamMemberRoleCommand(Guid Id) : ICommand;

public sealed class DeleteTeamMemberRoleCommandValidator : CustomValidator<DeleteTeamMemberRoleCommand>
{
    public DeleteTeamMemberRoleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeleteTeamMemberRoleCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<DeleteTeamMemberRoleCommandHandler> logger)
    : ICommandHandler<DeleteTeamMemberRoleCommand>
{
    private const string AppRequestName = nameof(DeleteTeamMemberRoleCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<DeleteTeamMemberRoleCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteTeamMemberRoleCommand request, CancellationToken cancellationToken)
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

            var inUse = await _organizationDbContext.TeamMembers
                .AnyAsync(m => m.RoleId == request.Id, cancellationToken);

            if (inUse)
            {
                _logger.LogInformation("Team member role {TeamMemberRoleId} is in use and cannot be deleted.", request.Id);
                return Result.Failure("This team member role is assigned to one or more team members and cannot be deleted.");
            }

            _organizationDbContext.TeamMemberRoles.Remove(role);
            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Team member role {TeamMemberRoleId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
