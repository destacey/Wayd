using Wayd.Organization.Application.Teams.Models;

namespace Wayd.Organization.Application.Teams.Commands;

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

public sealed class UpdateTeamMembershipCommandHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateTeamMembershipCommandHandler> logger) : ICommandHandler<UpdateTeamMembershipCommand>
{
    private const string RequestName = nameof(UpdateTeamMembershipCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateTeamMembershipCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateTeamMembershipCommand request, CancellationToken cancellationToken)
    {
        try
        {
            Team team = await _organizationDbContext.Teams
                .Include(t => t.ParentMemberships)
                    .ThenInclude(m => m.Target)
                .SingleAsync(t => t.Id == request.TeamId, cancellationToken: cancellationToken);

            var result = team.UpdateTeamMembership(request.TeamMembershipId, request.DateRange, _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogError("{RequestName}: failed to update Team Membership {TeamMembershipId} for Team {TeamId}. Error: {Error}", RequestName, request.TeamMembershipId, request.TeamId, result.Error);
                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("{RequestName}: updated Team Membership {TeamMembershipId} for Team {TeamId}", RequestName, request.TeamMembershipId, request.TeamId);

            // Sync the updated TeamMembership with the graph database
            // TODO: move to more of an event based approach
            await _organizationDbContext.UpsertTeamMembershipEdge(TeamMembershipEdge.From(result.Value), cancellationToken);

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