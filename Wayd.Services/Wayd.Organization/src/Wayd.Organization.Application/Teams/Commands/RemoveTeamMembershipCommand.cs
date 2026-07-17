namespace Wayd.Organization.Application.Teams.Commands;

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
    private readonly ILogger<RemoveTeamMembershipCommandHandler> _logger;

    public RemoveTeamMembershipCommandHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider, ILogger<RemoveTeamMembershipCommandHandler> logger)
    {
        _organizationDbContext = organizationDbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result> Handle(RemoveTeamMembershipCommand request, CancellationToken cancellationToken)
    {
        try
        {
            Team team = await _organizationDbContext.Teams
                .Include(t => t.ParentMemberships)
                    .ThenInclude(m => m.Target)
                .AsNoTracking() // needed until the EF Core bug below is fixed
                .SingleAsync(t => t.Id == request.TeamId);

            var result = team.RemoveTeamMembership(request.TeamMembershipId);
            if (result.IsFailure)
            {
                _logger.LogError("{RequestName}: failed to remove Team Membership {TeamMembershipId} for Team {TeamId}. Error: {Error}", RequestName, request.TeamMembershipId, request.TeamId, result.Error);
                return result;
            }

            /// Cleans up deleted team memberships.  This is needed because of a bug in EF Core 7.x.
            _organizationDbContext.Entry(result.Value).State = EntityState.Deleted;

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("{RequestName}: removed Team Membership {TeamMembershipId} for Team of Teams {TeamId}", RequestName, request.TeamMembershipId, request.TeamId);

            // Sync the deleted TeamMembership with the graph database
            // TODO: move to more of an event based approach
            await _organizationDbContext.DeleteTeamMembershipEdge(request.TeamMembershipId, cancellationToken);

            _logger.LogDebug("{RequestName}: synced TeamMembershipEdge for Team Membership {TeamMembershipId}", RequestName, request.TeamMembershipId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}: {@Request}", RequestName, request);

            return Result.Failure<int>($"Exception for request {RequestName} {request}");
        }
    }
}