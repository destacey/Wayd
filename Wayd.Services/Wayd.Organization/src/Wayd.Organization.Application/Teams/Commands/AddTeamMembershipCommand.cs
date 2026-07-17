using Wayd.Organization.Application.Teams.Models;

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

public sealed class AddTeamMembershipCommandHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider, ILogger<AddTeamMembershipCommandHandler> logger) : ICommandHandler<AddTeamMembershipCommand>
{
    private const string RequestName = nameof(AddTeamMembershipCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<AddTeamMembershipCommandHandler> _logger = logger;

    public async Task<Result> Handle(AddTeamMembershipCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var team = await _organizationDbContext.Teams
                .Include(t => t.ParentMemberships)
                .SingleAsync(t => t.Id == request.TeamId, cancellationToken: cancellationToken);

            var parentTeam = await _organizationDbContext.TeamOfTeams
                .SingleAsync(t => t.Id == request.ParentTeamId, cancellationToken: cancellationToken);

            var result = team.AddTeamMembership(parentTeam, request.DateRange, _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogError("{RequestName}: failed to add Team Membership for Team {TeamId} and ParentTeamId {ParentTeamId}. Error: {Error}", RequestName, request.TeamId, request.ParentTeamId, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("{RequestName}: added Team Membership {TeamMembershipId} for Team {TeamId} and ParentTeamId {ParentTeamId}", RequestName, result.Value.Id, request.TeamId, request.ParentTeamId);

            // Sync the new TeamMembership with the graph database
            // TODO: move to more of an event based approach
            await _organizationDbContext.UpsertTeamMembershipEdge(TeamMembershipEdge.From(result.Value), cancellationToken);

            _logger.LogDebug("{RequestName}: synced TeamMembershipEdge for Team Membership {TeamMembershipId}", RequestName, result.Value.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}: {@Request}", RequestName, request);

            return Result.Failure<int>($"Exception for request {RequestName} {request}");
        }
    }
}