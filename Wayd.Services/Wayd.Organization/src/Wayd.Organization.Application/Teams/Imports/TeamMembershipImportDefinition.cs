using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Persistence;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Models;

namespace Wayd.Organization.Application.Teams.Imports;

/// <summary>
/// Imports the team hierarchy: each row places a child (a Team or a Team of Teams) under a parent Team of
/// Teams, so a value stream / ART / team hierarchy can be built in one file.
/// </summary>
/// <remarks>
/// The only <see cref="ImportAtomicity.Atomic"/> import, and the only <see cref="ImportPassScope.WholeSet"/>
/// pass. Half an imported hierarchy is worse than none, and the correctness argument is concrete: the pass
/// loads every referenced team tracked with its memberships so EF's relationship fixup keeps both ends of
/// each new edge consistent, which is what the domain's cycle and overlap checks read. Chunk it and those
/// checks go blind to the half of the file they cannot see.
/// <para>
/// The second pass exists because the graph tables have to be written <em>after</em> the relational save.
/// Passes run in order with a save between them, so a pass is exactly the seam for that — it re-reads the
/// memberships rather than carrying them over, the same way the employee import finds its managers.
/// </para>
/// </remarks>
public sealed class TeamMembershipImportDefinition(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportTeamMembershipDto>(serializer)
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public const string ImportKey = "team-memberships";

    public override string Key => ImportKey;
    public override string DisplayName => "Team Hierarchy";
    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    public override string PermissionAction => ApplicationAction.ManageTeamMemberships;
    public override string PermissionResource => ApplicationResource.Teams;

    protected override IReadOnlyList<ImportPass<ImportTeamMembershipDto>> Steps =>
    [
        new("AddMemberships", ImportPassScope.WholeSet, AddMemberships),
        new("SyncGraphEdges", ImportPassScope.WholeSet, SyncGraphEdges),
    ];

    /// <summary>
    /// Resolves every reference and checks every parent before adding a single edge.
    /// </summary>
    /// <remarks>
    /// Validating ahead of mutating is the contract an atomic definition owes the runner: when a row is
    /// rejected the run is failed without anything being undone, which only holds if nothing was done.
    /// Rejections are recorded per row even so, because "which row is wrong" is what the person needs.
    /// </remarks>
    private async Task<Result> AddMemberships(ImportPassContext<ImportTeamMembershipDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        var codes = context.Rows
            .SelectMany(r => new[] { Normalize(r.Data.ChildCode), Normalize(r.Data.ParentCode) })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Compare against TeamCode instances, never t.Code.Value: Code is a value converter, so the property
        // translates but a member of it does not.
        var codeValues = codes.Select(c => new TeamCode(c)).ToList();

        var teamsByCode = (await _organizationDbContext.BaseTeams
                .Include(t => t.ParentMemberships)
                .Where(t => codeValues.Contains(t.Code))
                .ToListAsync(cancellationToken))
            .ToDictionary(t => t.Code.Value, t => t, StringComparer.OrdinalIgnoreCase);

        var rejected = false;

        foreach (var row in context.Rows)
        {
            var childCode = Normalize(row.Data.ChildCode);
            var parentCode = Normalize(row.Data.ParentCode);

            if (!teamsByCode.TryGetValue(childCode, out _) || !teamsByCode.ContainsKey(parentCode))
            {
                var missing = !teamsByCode.ContainsKey(childCode) ? childCode : parentCode;
                row.Failed($"No team exists with the code '{missing}'.");
                rejected = true;
                continue;
            }

            if (teamsByCode[parentCode] is not TeamOfTeams)
            {
                row.Failed($"'{parentCode}' is a Team, so it cannot be a parent. Only a Team of Teams can be.");
                rejected = true;
            }
        }

        // Nothing has been mutated yet, so stopping here leaves nothing to undo.
        if (rejected)
            return Result.Success();

        foreach (var row in context.Rows)
        {
            var child = teamsByCode[Normalize(row.Data.ChildCode)];
            var parent = (TeamOfTeams)teamsByCode[Normalize(row.Data.ParentCode)];

            var result = child.AddTeamMembership(parent, new MembershipDateRange(row.Data.Start, row.Data.End), timestamp);
            if (result.IsFailure)
            {
                // The domain refused — an overlap or a cycle it can only see with the whole file loaded.
                // Reported against the row; the runner keeps the rest of the file out because this import
                // is atomic, and the edges added before this point are discarded with the transaction.
                row.Failed($"Could not place '{row.Data.ChildCode}' under '{row.Data.ParentCode}': {result.Error}");
                return Result.Success();
            }

            row.Created(result.Value.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Mirrors each new edge into the graph tables, after the relational save the runner performs between
    /// passes. Mirrors the single-item AddTeamMembership handlers.
    /// </summary>
    /// <remarks>
    /// TODO: move to an event-based approach, the same TODO the single-item handlers carry.
    /// </remarks>
    private async Task<Result> SyncGraphEdges(ImportPassContext<ImportTeamMembershipDto> context, CancellationToken cancellationToken)
    {
        var membershipIds = context.Accepted
            .Select(r => r.CreatedEntityId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToHashSet();

        if (membershipIds.Count == 0)
            return Result.Success();

        // Reached through the teams rather than a memberships DbSet, which the context does not expose.
        // Both ends are needed anyway: the edge is built from the child and parent, not from the
        // membership's navigations, which is what the single-item handlers do for the same reason.
        var codes = context.Accepted
            .SelectMany(r => new[] { Normalize(r.Data.ChildCode), Normalize(r.Data.ParentCode) })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var codeValues = codes.Select(c => new TeamCode(c)).ToList();

        var teamsByCode = (await _organizationDbContext.BaseTeams
                .Include(t => t.ParentMemberships)
                .Where(t => codeValues.Contains(t.Code))
                .ToListAsync(cancellationToken))
            .ToDictionary(t => t.Code.Value, t => t, StringComparer.OrdinalIgnoreCase);

        foreach (var row in context.Accepted)
        {
            var child = teamsByCode[Normalize(row.Data.ChildCode)];
            var parent = (TeamOfTeams)teamsByCode[Normalize(row.Data.ParentCode)];
            var membership = child.ParentMemberships.SingleOrDefault(m => m.Id == row.CreatedEntityId);

            if (membership is null)
                return Result.Failure($"The membership created for '{row.Data.ChildCode}' could not be read back.");

            await _organizationDbContext.UpsertTeamMembershipEdge(
                TeamMembershipEdge.From(membership, child, parent), cancellationToken);
        }

        return Result.Success();
    }

    private static string Normalize(string teamCode) => teamCode.Trim().ToUpperInvariant();
}
