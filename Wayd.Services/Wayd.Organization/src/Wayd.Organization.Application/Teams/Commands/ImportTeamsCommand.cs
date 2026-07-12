using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.Teams.Models;
using Wayd.Organization.Domain.Enums;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Commands;

/// <summary>
/// Additively imports a batch of Teams and Teams of Teams through the domain factories, so a single
/// SaveChanges dispatches the creation events that replicate each team into the PPM, Planning and Work
/// projections (same Id). The two team kinds are discriminated per row by <see cref="ImportTeamDto.Type"/>.
/// After persistence, each new team is synced into the graph tables one row at a time, mirroring the
/// single-create command handlers.
/// </summary>
public sealed record ImportTeamsCommand : ICommand
{
    public ImportTeamsCommand(IEnumerable<ImportTeamDto> teams)
    {
        Teams = [.. teams];
    }

    public List<ImportTeamDto> Teams { get; }
}

public sealed class ImportTeamsCommandValidator : CustomValidator<ImportTeamsCommand>
{
    public ImportTeamsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Teams)
            .NotNull()
            .NotEmpty()
            .Must(t => t.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == t.Count)
                .WithMessage("Team Name must be unique.")
            .Must(t => t.Select(x => x.Code.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() == t.Count)
                .WithMessage("Team Code must be unique.");

        RuleForEach(t => t.Teams)
            .NotNull()
            .SetValidator(new ImportTeamDtoValidator());
    }
}

internal sealed class ImportTeamsCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<ImportTeamsCommandHandler> logger) : ICommandHandler<ImportTeamsCommand>
{
    private const string RequestName = nameof(ImportTeamsCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ImportTeamsCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportTeamsCommand request, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        try
        {
            var created = new List<BaseTeam>(request.Teams.Count);

            foreach (var row in request.Teams)
            {
                BaseTeam team;

                if (row.Type == TeamType.TeamOfTeams)
                {
                    var teamOfTeams = TeamOfTeams.Create(row.Name, row.Code, row.Description, row.ActiveDate, timestamp);
                    await _organizationDbContext.TeamOfTeams.AddAsync(teamOfTeams, cancellationToken);
                    team = teamOfTeams;
                }
                else
                {
                    // Match the single-create default operating model (Kanban + Count).
                    var newTeam = Team.Create(row.Name, row.Code, row.Description, row.ActiveDate, Methodology.Kanban, SizingMethod.Count, timestamp);
                    await _organizationDbContext.Teams.AddAsync(newTeam, cancellationToken);
                    team = newTeam;
                }

                // A row may represent an already-retired team. It is created active (the only way the domain
                // allows) and then deactivated through the same behavior the UI uses, so the deactivation
                // event fires. A freshly-created team has no memberships, so the only rule Deactivate can
                // trip here is AsOfDate > ActiveDate, which the DTO validator has already enforced.
                if (!row.IsActive)
                {
                    var deactivateResult = Deactivate(team, row.InactiveDate!.Value, timestamp);
                    if (deactivateResult.IsFailure)
                    {
                        _logger.LogWarning("{RequestName}: failed to deactivate imported team {TeamCode}: {Error}", RequestName, row.Code.Value, deactivateResult.Error);
                        return Result.Failure(deactivateResult.Error);
                    }
                }

                created.Add(team);
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            // Sync each new team into the graph database. Done after the relational save so the row exists,
            // mirroring CreateTeamCommandHandler / CreateTeamOfTeamsCommandHandler.
            // TODO: move to more of an event based approach (same TODO as the single-create handlers).
            foreach (var team in created)
            {
                await _organizationDbContext.UpsertTeamNode(TeamNode.From(team), cancellationToken);
            }

            _logger.LogInformation("{RequestName}: imported {Count} team(s).", RequestName, created.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}");
        }
    }

    // Team and TeamOfTeams each define their own Deactivate(TeamDeactivatableArgs); there is no shared
    // BaseTeam method, so dispatch on the concrete type.
    private static Result Deactivate(BaseTeam team, LocalDate inactiveDate, Instant timestamp)
    {
        var args = TeamDeactivatableArgs.Create(inactiveDate, timestamp);

        return team switch
        {
            TeamOfTeams teamOfTeams => teamOfTeams.Deactivate(args),
            Team plainTeam => plainTeam.Deactivate(args),
            _ => Result.Failure($"Unsupported team type '{team.GetType().Name}'."),
        };
    }
}
