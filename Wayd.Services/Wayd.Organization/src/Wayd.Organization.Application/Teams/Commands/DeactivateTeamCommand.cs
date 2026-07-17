using Wayd.Organization.Application.Teams.Models;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Commands;

public sealed record DeactivateTeamCommand(Guid Id, LocalDate InactiveDate) : ICommand;

public sealed class DeactivateTeamCommandValidator : CustomValidator<DeactivateTeamCommand>
{
    public DeactivateTeamCommandValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty();

        RuleFor(t => t.InactiveDate)
            .NotEmpty();
    }
}

public sealed class DeactivateTeamCommandHandler(IOrganizationDbContext organizationDbContext, ILogger<DeactivateTeamCommand> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<DeactivateTeamCommand>
{
    private const string RequestName = nameof(DeactivateTeamCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<DeactivateTeamCommand> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeactivateTeamCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var team = await _organizationDbContext.Teams
                .Include(t => t.ParentMemberships)
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            if (team is null)
            {
                _logger.LogInformation("{RequestName}: Team not found. {@Request}", RequestName, request);
                return Result.Failure("Team not found.");
            }

            var result = team.Deactivate(TeamDeactivatableArgs.Create(request.InactiveDate, _dateTimeProvider.Now));
            if (result.IsFailure)
            {
                // Reset the entity
                await _organizationDbContext.Entry(team).ReloadAsync(cancellationToken);
                team.ClearDomainEvents();

                _logger.LogError("{RequestName}: failed to deactivate Team {TeamId}. Error: {Error}", RequestName, team.Id, result.Error);

                return result;
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: deactivated Team {TeamId}", RequestName, team.Id);

            // Sync the new team with the graph database
            // TODO: move to more of an event based approach
            await _organizationDbContext.UpsertTeamNode(TeamNode.From(team), cancellationToken);

            _logger.LogDebug("{RequestName}: synced TeamNode for Team with Id {TeamId}, Key {TeamKey}, and Code {TeamCode}", RequestName, team.Id, team.Key, team.Code);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}: {@Request}", RequestName, request);

            return Result.Failure<int>($"Exception for request {RequestName} {request}");
        }
    }
}
