using NodaTime;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// A team and every team beneath it at any point from <paramref name="From"/> to <paramref name="To"/>
/// inclusive, or null when the team does not exist.
/// </summary>
/// <remarks>
/// The hierarchy comes back as dated edges rather than one resolved tree: a team can move between parents
/// inside the window, and a reader placing an event (a completed work item, say) needs the parent on that
/// event's date.
/// </remarks>
public sealed record GetTeamStructureQuery(Guid TeamId, LocalDate From, LocalDate To) : IQuery<TeamStructure?>;

public sealed class GetTeamStructureQueryValidator : CustomValidator<GetTeamStructureQuery>
{
    public GetTeamStructureQueryValidator()
    {
        RuleFor(q => q.TeamId)
            .NotEmpty();

        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From);
    }
}

/// <param name="RootId">The team the structure was requested for.</param>
/// <param name="Teams">The root and every team reachable beneath it through memberships overlapping the window.</param>
/// <param name="Memberships">Each membership between those teams that overlaps the window, with its full date range.</param>
/// <param name="SizingPeriods">
/// Each operating model of a <see cref="TeamType.Team"/> in <paramref name="Teams"/> that overlaps the window.
/// Teams of teams have none.
/// </param>
public sealed record TeamStructure(
    Guid RootId,
    IReadOnlyList<TeamStructureTeam> Teams,
    IReadOnlyList<TeamStructureMembership> Memberships,
    IReadOnlyList<TeamSizingPeriod> SizingPeriods);

public sealed record TeamStructureTeam(Guid Id, int Key, string Code, string Name, TeamType Type);

/// <param name="End">Inclusive; null while the membership is open-ended.</param>
public sealed record TeamStructureMembership(Guid ChildId, Guid ParentId, LocalDate Start, LocalDate? End)
{
    public bool IncludesDate(LocalDate date) => Start <= date && (End is null || date <= End);
}

/// <param name="End">Inclusive; null for the current operating model.</param>
/// <param name="UsesStoryPoints">False when the team sizes by count, where every item counts as one.</param>
public sealed record TeamSizingPeriod(Guid TeamId, LocalDate Start, LocalDate? End, bool UsesStoryPoints)
{
    public bool IncludesDate(LocalDate date) => Start <= date && (End is null || date <= End);
}
