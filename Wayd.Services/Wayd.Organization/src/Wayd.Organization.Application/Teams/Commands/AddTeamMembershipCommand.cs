
namespace Wayd.Organization.Application.Teams.Commands;

public sealed record AddTeamMembershipCommand(Guid TeamId, Guid ParentTeamId, MembershipDateRange DateRange) : ICommand;

public sealed class AddTeamMembershipCommandValidator : CustomValidator<AddTeamMembershipCommand>
{
    public AddTeamMembershipCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.TeamId)
            .NotEmpty();

        RuleFor(t => t.ParentTeamId)
            .NotEmpty();

        RuleFor(t => t.DateRange)
            .NotNull();
    }
}

public sealed class AddTeamMembershipCommandHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider, ICurrentUser currentUser, ILogger<AddTeamMembershipCommandHandler> logger) : ICommandHandler<AddTeamMembershipCommand>
{
    private const string RequestName = nameof(AddTeamMembershipCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<AddTeamMembershipCommandHandler> _logger = logger;

    public async Task<Result> Handle(AddTeamMembershipCommand request, CancellationToken cancellationToken)
    {
        var team = await _organizationDbContext.Teams
            .Include(t => t.ParentMemberships)
            .SingleOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);
        if (team is null)
            return Result.Failure("Team not found.");

        var parentTeam = await _organizationDbContext.TeamOfTeams
            .SingleOrDefaultAsync(t => t.Id == request.ParentTeamId, cancellationToken);
        if (parentTeam is null)
            return Result.Failure("Parent team of teams not found.");

        var now = _dateTimeProvider.Now;
        var result = team.AddTeamMembership(parentTeam, request.DateRange, EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), now);
        if (result.IsFailure)
        {
            _logger.LogError("{RequestName}: failed to add Team Membership for Team {TeamId} and ParentTeamId {ParentTeamId}. Error: {Error}", RequestName, request.TeamId, request.ParentTeamId, result.Error);
            return result;
        }

        await _organizationDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("{RequestName}: added Team Membership {TeamMembershipId} for Team {TeamId} and ParentTeamId {ParentTeamId}", RequestName, result.Value.Id, request.TeamId, request.ParentTeamId);

        return Result.Success();
    }
}