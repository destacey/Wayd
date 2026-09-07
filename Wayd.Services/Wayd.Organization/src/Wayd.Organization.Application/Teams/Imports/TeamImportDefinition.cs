using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Persistence;
using Wayd.Organization.Application.Teams.Models;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;

namespace Wayd.Organization.Application.Teams.Imports;

/// <summary>
/// Imports teams and teams of teams, discriminated per row by <see cref="ImportTeamDto.Type"/>.
/// </summary>
/// <remarks>
/// Atomic, matching the single save the command it replaces did. Creating a team publishes events that
/// replicate it into the PPM, Planning and Work projections, so a half-applied file leaves those areas
/// holding half an organization.
/// <para>
/// The second pass exists because the graph tables have to be written <em>after</em> the relational save —
/// a node row cannot reference a team that is not there yet. Passes run in order with a save between them,
/// which is exactly that seam; it re-reads the teams by code rather than carrying them over.
/// </para>
/// </remarks>
public sealed class TeamImportDefinition(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportTeamDto>(serializer)
{
    public const string ImportKey = "teams";

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;

    public override string Key => ImportKey;
    public override string DisplayName => "Teams";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Teams;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportTeamDto>> Steps =>
    [
        new("CreateTeams", ImportPassScope.WholeSet, CreateTeams),
        new("SyncGraphNodes", ImportPassScope.WholeSet, SyncGraphNodes),
    ];

    /// <summary>
    /// Rejects a row whose name or code is already taken, then creates what is left.
    /// </summary>
    /// <remarks>
    /// Both are unique indexes, so a collision used to surface as a constraint violation when the batch
    /// saved — an exception naming a column, taking every good row with it. Checking here names the row
    /// instead. Uniqueness <em>within</em> the file is the submission command's rule, since it is a
    /// property of the file rather than of any one row.
    /// </remarks>
    private async Task<Result> CreateTeams(ImportPassContext<ImportTeamDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person edited every row by
        // hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        // Compare against TeamCode instances, never t.Code.Value: Code is a value converter, so the
        // property translates but a member of it does not.
        var codes = context.Rows.Select(r => r.Data.Code).ToList();
        var names = context.Rows.Select(r => r.Data.Name).ToList();

        var takenCodes = (await _organizationDbContext.BaseTeams
                .AsNoTracking()
                .Where(t => codes.Contains(t.Code))
                .Select(t => t.Code)
                .ToListAsync(cancellationToken))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var takenNames = (await _organizationDbContext.BaseTeams
                .AsNoTracking()
                .Where(t => names.Contains(t.Name))
                .Select(t => t.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;

            if (takenCodes.Contains(data.Code.Value))
            {
                row.Failed($"A team already exists with code '{data.Code.Value}'.");
                continue;
            }

            if (takenNames.Contains(data.Name))
            {
                row.Failed($"A team already exists named '{data.Name}'.");
                continue;
            }

            BaseTeam team;

            if (data.Type == TeamType.TeamOfTeams)
            {
                var teamOfTeams = TeamOfTeams.Create(data.Name, data.Code, data.Description, data.ActiveDate, actor, timestamp);
                await _organizationDbContext.TeamOfTeams.AddAsync(teamOfTeams, cancellationToken);
                team = teamOfTeams;
            }
            else
            {
                // Match the single-create default operating model (Kanban + Count).
                var plainTeam = Team.Create(
                    data.Name, data.Code, data.Description, data.ActiveDate,
                    Methodology.Kanban, SizingMethod.Count, actor, timestamp);

                await _organizationDbContext.Teams.AddAsync(plainTeam, cancellationToken);
                team = plainTeam;
            }

            // A row may represent an already-retired team. It is created active — the only way the domain
            // allows — and then deactivated through the same behavior the UI uses, so the deactivation
            // event fires. A freshly-created team has no memberships, so the only rule Deactivate can trip
            // is AsOfDate > ActiveDate, which the row validator has already enforced.
            if (!data.IsActive)
            {
                var deactivated = Deactivate(team, data.InactiveDate!.Value, actor, timestamp);
                if (deactivated.IsFailure)
                {
                    row.Failed(deactivated.Error);
                    continue;
                }
            }

            row.Created(team.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Mirrors each created team into the graph tables, now that the relational rows exist.
    /// </summary>
    private async Task<Result> SyncGraphNodes(ImportPassContext<ImportTeamDto> context, CancellationToken cancellationToken)
    {
        var createdIds = context.Accepted
            .Where(r => r.CreatedEntityId.HasValue)
            .Select(r => r.CreatedEntityId!.Value)
            .ToList();

        if (createdIds.Count == 0)
            return Result.Success();

        var teams = await _organizationDbContext.BaseTeams
            .AsNoTracking()
            .Where(t => createdIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        foreach (var team in teams)
        {
            await _organizationDbContext.UpsertTeamNode(TeamNode.From(team), cancellationToken);
        }

        return Result.Success();
    }

    // Team and TeamOfTeams each define their own Deactivate(TeamDeactivatableArgs); there is no shared
    // BaseTeam method, so dispatch on the concrete type.
    private static Result Deactivate(BaseTeam team, LocalDate inactiveDate, EventActor actor, Instant timestamp)
    {
        var args = TeamDeactivatableArgs.Create(inactiveDate, actor, timestamp);

        return team switch
        {
            TeamOfTeams teamOfTeams => teamOfTeams.Deactivate(args),
            Team plainTeam => plainTeam.Deactivate(args),
            _ => Result.Failure($"Unsupported team type '{team.GetType().Name}'."),
        };
    }
}
