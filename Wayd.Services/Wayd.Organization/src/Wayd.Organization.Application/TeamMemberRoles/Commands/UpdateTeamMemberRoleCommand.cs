namespace Wayd.Organization.Application.TeamMemberRoles.Commands;

public sealed record UpdateTeamMemberRoleCommand(Guid Id, string Name, string? Description) : ICommand;

public sealed class UpdateTeamMemberRoleCommandValidator : CustomValidator<UpdateTeamMemberRoleCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext;

    public UpdateTeamMemberRoleCommandValidator(IOrganizationDbContext organizationDbContext)
    {
        _organizationDbContext = organizationDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(BeUniqueName).WithMessage("A team member role with this name already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(1024)
            .When(c => c.Description is not null);
    }

    private async Task<bool> BeUniqueName(UpdateTeamMemberRoleCommand command, string name, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();
        return await _organizationDbContext.TeamMemberRoles
            .AllAsync(r => r.Id == command.Id || r.Name != normalized, cancellationToken);
    }
}

public sealed class UpdateTeamMemberRoleCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<UpdateTeamMemberRoleCommandHandler> logger)
    : ICommandHandler<UpdateTeamMemberRoleCommand>
{
    private const string AppRequestName = nameof(UpdateTeamMemberRoleCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<UpdateTeamMemberRoleCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateTeamMemberRoleCommand request, CancellationToken cancellationToken)
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

            var result = role.Update(request.Name, request.Description);
            if (result.IsFailure)
            {
                _logger.LogError("Error updating team member role {TeamMemberRoleId}. Error message: {Error}", request.Id, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Team member role {TeamMemberRoleId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
