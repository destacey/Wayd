using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Models;

namespace Wayd.Organization.Application.Teams.Commands;

public sealed record UpdateTeamCommand(Guid Id, string Name, TeamCode Code, string? Description) : ICommand<int>;

public sealed class UpdateTeamCommandValidator : CustomValidator<UpdateTeamCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext;

    public UpdateTeamCommandValidator(IOrganizationDbContext organizationDbContext)
    {
        _organizationDbContext = organizationDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(async (model, name, cancellationToken) =>
            {
                return await BeUniqueTeamName(model.Id, name, cancellationToken);
            }).WithMessage("The Team name already exists.");

        RuleFor(t => t.Code)
            .NotEmpty()
            .SetValidator(t => new TeamCodeValidator(_organizationDbContext, t.Id));

        RuleFor(t => t.Description)
            .MaximumLength(1024);
    }

    public async Task<bool> BeUniqueTeamName(Guid id, string name, CancellationToken cancellationToken)
    {
        return await _organizationDbContext.BaseTeams.Where(t => t.Id != id).AllAsync(x => x.Name != name, cancellationToken);
    }
}

public sealed class UpdateTeamCommandHandler : ICommandHandler<UpdateTeamCommand, int>
{
    private const string RequestName = nameof(UpdateTeamCommand);

    private readonly IOrganizationDbContext _organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UpdateTeamCommandHandler> _logger;

    public UpdateTeamCommandHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateTeamCommandHandler> logger)
    {
        _organizationDbContext = organizationDbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var team = await _organizationDbContext.Teams
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
            if (team is null)
                return Result.Failure<int>("Team not found.");

            var updateResult = team.Update(
                request.Name,
                request.Code,
                request.Description,
                _dateTimeProvider.Now
                );

            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _organizationDbContext.Entry(team).ReloadAsync(cancellationToken);
                team.ClearDomainEvents();

                _logger.LogError("Failure for request {RequestName}: {@Request}.  Error message: {Error}", RequestName, request, updateResult.Error);

                return Result.Failure<int>(updateResult.Error);
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("{RequestName}: updated Team with Id {TeamId}, Key {TeamKey}, and Code {TeamCode}", RequestName, team.Id, team.Key, team.Code);

            // Sync the new team with the graph database
            // TODO: move to more of an event based approach
            await _organizationDbContext.UpsertTeamNode(TeamNode.From(team), cancellationToken);

            _logger.LogDebug("{RequestName}: synced TeamNode for Team with Id {TeamId}, Key {TeamKey}, and Code {TeamCode}", RequestName, team.Id, team.Key, team.Code);

            return Result.Success(team.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}: {@Request}", RequestName, request);

            return Result.Failure<int>($"Exception for request {RequestName} {request}");
        }
    }
}

