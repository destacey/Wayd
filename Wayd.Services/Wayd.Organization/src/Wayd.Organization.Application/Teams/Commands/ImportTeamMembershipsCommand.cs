using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Models;

namespace Wayd.Organization.Application.Teams.Commands;

/// <summary>
/// Additively imports the team hierarchy from a batch of rows, each placing a child (a Team or a Team of
/// Teams) under a parent Team of Teams by natural key. Because the parent may itself be a Team of Teams, a
/// three-tier value-stream / ART / team hierarchy can be imported. Each edge is added through the domain
/// <c>AddTeamMembership</c> path (a single SaveChanges dispatches the changes), then synced into the graph
/// tables one row at a time — mirroring the single-item AddTeamMembership handlers. The batch is
/// all-or-nothing: if any child/parent code cannot be resolved (or the parent is not a Team of Teams),
/// nothing is persisted and the unresolved references are returned, so seeding stays re-runnable.
/// </summary>
public sealed record ImportTeamMembershipsCommand : ICommand
{
    public ImportTeamMembershipsCommand(IEnumerable<ImportTeamMembershipDto> memberships)
    {
        Memberships = [.. memberships];
    }

    public List<ImportTeamMembershipDto> Memberships { get; }
}

public sealed class ImportTeamMembershipsCommandValidator : CustomValidator<ImportTeamMembershipsCommand>
{
    public ImportTeamMembershipsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(m => m.Memberships)
            .NotNull()
            .NotEmpty();

        RuleForEach(m => m.Memberships)
            .NotNull()
            .SetValidator(new ImportTeamMembershipDtoValidator());
    }
}

public sealed class ImportTeamMembershipsCommandHandler(
    IOrganizationDbContext organizationDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<ImportTeamMembershipsCommandHandler> logger) : ICommandHandler<ImportTeamMembershipsCommand>
{
    private const string RequestName = nameof(ImportTeamMembershipsCommand);

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ImportTeamMembershipsCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportTeamMembershipsCommand request, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        try
        {
            var codes = request.Memberships
                .SelectMany(m => new[] { Normalize(m.ChildCode), Normalize(m.ParentCode) })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // TeamCode is persisted via a value converter to a single column, so compare against TeamCode
            // instances (t.Code, never t.Code.Value, which does not translate). Load every referenced team as
            // a tracked entity with its memberships so the domain's overlap/cycle checks see intra-batch edges,
            // and so relationship fixup keeps both ends consistent across the batch.
            var codeValues = codes.Select(c => new TeamCode(c)).ToList();

            // Track every referenced team with its parent memberships. Both ends of each edge we add below are
            // tracked, so EF relationship fixup keeps the parents' ChildMemberships consistent within the batch
            // — which is what the domain's cycle check reads. (Seeding runs against a fresh hierarchy, so there
            // are no pre-existing edges to miss.)
            var teamsByCode = (await _organizationDbContext.BaseTeams
                    .Include(t => t.ParentMemberships)
                    .Where(t => codeValues.Contains(t.Code))
                    .ToListAsync(cancellationToken))
                .ToDictionary(t => t.Code.Value, t => t, StringComparer.OrdinalIgnoreCase);

            var unresolved = codes.Where(c => !teamsByCode.ContainsKey(c)).Select(c => $"team code '{c}'").ToList();
            if (unresolved.Count > 0)
                return Fail($"Could not resolve the following references: {string.Join(", ", unresolved)}.");

            // Validate parent types up front so a bad row fails the whole batch cleanly.
            var notTeamsOfTeams = request.Memberships
                .Select(m => Normalize(m.ParentCode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(code => teamsByCode[code] is not TeamOfTeams)
                .ToList();
            if (notTeamsOfTeams.Count > 0)
                return Fail($"The following parent teams are not Teams of Teams: {string.Join(", ", notTeamsOfTeams.Select(c => $"'{c}'"))}.");

            var addedEdges = new List<(TeamMembership Membership, BaseTeam Child, TeamOfTeams Parent)>(request.Memberships.Count);

            foreach (var row in request.Memberships)
            {
                var child = teamsByCode[Normalize(row.ChildCode)];
                var parent = (TeamOfTeams)teamsByCode[Normalize(row.ParentCode)];

                var result = child.AddTeamMembership(parent, new MembershipDateRange(row.Start, row.End), timestamp);
                if (result.IsFailure)
                    return Fail($"Could not add {row.ChildCode} under {row.ParentCode}: {result.Error}");

                addedEdges.Add((result.Value, child, parent));
            }

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            // Sync each new edge into the graph database after the relational save, mirroring the
            // single-item AddTeamMembership handlers. Build the edge from the child/parent we already hold so
            // it does not depend on EF fixup having populated the membership's Source/Target navigations.
            // TODO: move to more of an event based approach (same TODO as the single-item handlers).
            foreach (var (membership, child, parent) in addedEdges)
            {
                await _organizationDbContext.UpsertTeamMembershipEdge(TeamMembershipEdge.From(membership, child, parent), cancellationToken);
            }

            _logger.LogInformation("{RequestName}: imported {Count} team membership(s).", RequestName, addedEdges.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }

    private Result Fail(string message)
    {
        _logger.LogWarning("{RequestName}: {Message}", RequestName, message);
        return Result.Failure(message);
    }

    private static string Normalize(string teamCode) => teamCode.Trim().ToUpperInvariant();
}
