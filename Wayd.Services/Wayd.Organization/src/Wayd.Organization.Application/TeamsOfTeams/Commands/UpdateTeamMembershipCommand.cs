
namespace Wayd.Organization.Application.TeamsOfTeams.Commands;

public sealed record UpdateTeamMembershipCommand(Guid TeamId, Guid TeamMembershipId, MembershipDateRange DateRange) : ICommand;

public sealed class UpdateTeamMembershipCommandValidator : CustomValidator<UpdateTeamMembershipCommand>
{
    public UpdateTeamMembershipCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.TeamId)
            .NotEmpty();

        RuleFor(t => t.TeamMembershipId)
            .NotEmpty();

        RuleFor(t => t.DateRange)
            .NotNull();
    }
}

public sealed class UpdateTeamMembershipCommandHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider, ICurrentUser currentUser, ILogger<UpdateTeamMembershipCommandHandler> logger) : ICommandHandler<UpdateTeamMembershipCommand>
{
    private const string RequestName = nameof(UpdateTeamMembershipCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateTeamMembershipCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateTeamMembershipCommand request, CancellationToken cancellationToken)
    {
        var team = await _organizationDbContext.TeamOfTeams
            .Include(t => t.ParentMemberships)
                .ThenInclude(m => m.Target)
            .SingleOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);
        if (team is null)
            return Result.Failure("Team of teams not found.");

        await TeamOfTeamsDescendants.Load(_organizationDbContext, team, cancellationToken);

        var result = team.UpdateTeamMembership(request.TeamMembershipId, request.DateRange, EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
        if (result.IsFailure)
        {
            _logger.LogError("{RequestName}: failed to update Team Membership {TeamMembershipId} for Team of Teams {TeamId}. Error: {Error}", RequestName, request.TeamMembershipId, request.TeamId, result.Error);
            return result;
        }

        await _organizationDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("{RequestName}: updated Team Membership {TeamMembershipId} for Team of Teams {TeamId}", RequestName, request.TeamMembershipId, request.TeamId);

        return Result.Success();
    }
}