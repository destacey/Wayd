namespace Wayd.Organization.Application.TeamMemberRoles.Commands;

public sealed record CreateTeamMemberRoleCommand(string Name, string? Description) : ICommand<Guid>;

public sealed class CreateTeamMemberRoleCommandValidator : CustomValidator<CreateTeamMemberRoleCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext;

    public CreateTeamMemberRoleCommandValidator(IOrganizationDbContext organizationDbContext)
    {
        _organizationDbContext = organizationDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(BeUniqueName).WithMessage("A team member role with this name already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(1024)
            .When(c => c.Description is not null);
    }

    private async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();
        return await _organizationDbContext.TeamMemberRoles
            .AllAsync(r => r.Name != normalized, cancellationToken);
    }
}

public sealed class CreateTeamMemberRoleCommandHandler(
    IOrganizationDbContext organizationDbContext,
    ILogger<CreateTeamMemberRoleCommandHandler> logger)
    : ICommandHandler<CreateTeamMemberRoleCommand, Guid>
{
    private const string AppRequestName = nameof(CreateTeamMemberRoleCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<CreateTeamMemberRoleCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateTeamMemberRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = TeamMemberRole.Create(request.Name, request.Description);
            if (result.IsFailure)
            {
                _logger.LogError("Error creating team member role {Name}. Error message: {Error}", request.Name, result.Error);
                return Result.Failure<Guid>(result.Error);
            }

            await _organizationDbContext.TeamMemberRoles.AddAsync(result.Value, cancellationToken);
            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Team member role {TeamMemberRoleId} created with name {Name}.", result.Value.Id, request.Name);

            return Result.Success(result.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
