namespace Wayd.Organization.Application.TeamsOfTeams.Commands;

public sealed record RemoveTeamMembershipCommand(Guid TeamId, Guid TeamMembershipId) : ICommand;

public sealed class RemoveTeamMembershipCommandValidator : CustomValidator<RemoveTeamMembershipCommand>
{
    public RemoveTeamMembershipCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.TeamId)
            .NotEmpty();

        RuleFor(t => t.TeamMembershipId)
            .NotEmpty();
    }
}

public sealed class RemoveTeamMembershipCommandHandler : ICommandHandler<RemoveTeamMembershipCommand>
{
    private const string RequestName = nameof(RemoveTeamMembershipCommand);

    private readonly IOrganizationDbContext _organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<RemoveTeamMembershipCommandHandler> _logger;

    public RemoveTeamMembershipCommandHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider, ICurrentUser currentUser, ILogger<RemoveTeamMembershipCommandHandler> logger)
    {
        _organizationDbContext = organizationDbContext;
        _dateTimeProvider = dateTimeProvider;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result> Handle(RemoveTeamMembershipCommand request, CancellationToken cancellationToken)
    {
        try
        {
            TeamOfTeams team = await _organizationDbContext.TeamOfTeams
                .Include(t => t.ParentMemberships.Where(m => m.Id == request.TeamMembershipId))
                    .ThenInclude(m => m.Target)
                .AsNoTracking() // needed until the EF Core bug below is fixed
                .SingleAsync(t => t.Id == request.TeamId, cancellationToken: cancellationToken);

            var result = team.RemoveTeamMembership(request.TeamMembershipId, EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogError("{RequestName}: failed to remove Team Membership {TeamMembershipId} for Team of Teams {TeamId}. Error: {Error}", RequestName, request.TeamMembershipId, request.TeamId, result.Error);
                return result;
            }

            // The team was loaded untracked, and SaveChanges drains events only from tracked entities, so track
            // it or the removal leaves no activity. Entry().State tracks that one entity, not its graph.
            _organizationDbContext.Entry(team).State = EntityState.Unchanged;

            /// Cleans up deleted team memberships.  This is needed because of a bug in EF Core 7.x.
            _organizationDbContext.Entry(result.Value).State = EntityState.Deleted;

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("{RequestName}: removed Team Membership {TeamMembershipId} for Team of Teams {TeamId}", RequestName, request.TeamMembershipId, request.TeamId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}: {@Request}", RequestName, request);

            return Result.Failure<int>($"Exception for request {RequestName} {request}");
        }
    }
}