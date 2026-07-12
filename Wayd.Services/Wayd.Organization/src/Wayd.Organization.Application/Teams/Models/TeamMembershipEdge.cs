using NodaTime;

namespace Wayd.Organization.Application.Teams.Models;

public sealed record TeamMembershipEdge
{
    public Guid Id { get; set; }
    public LocalDate StartDate { get; set; }
    public LocalDate? EndDate { get; set; }

    /// <summary>
    /// The child team in the relationship.
    /// </summary>
    public TeamNode FromNode { get; set; } = null!;

    /// <summary>
    /// The parent team in the relationship.    
    /// </summary>
    public TeamNode ToNode { get; set; } = null!;

    public static TeamMembershipEdge From(TeamMembership membership)
    {
        return new TeamMembershipEdge
        {
            Id = membership.Id,
            StartDate = membership.DateRange.Start,
            EndDate = membership.DateRange.End,
            FromNode = TeamNode.From(membership.Source),
            ToNode = TeamNode.From(membership.Target)
        };
    }

    /// <summary>
    /// Builds the edge from an explicit child and parent rather than the membership's Source/Target
    /// navigations. Use this when the caller already holds both teams and does not want to depend on EF
    /// relationship fixup having populated the navigations (e.g. bulk import).
    /// </summary>
    public static TeamMembershipEdge From(TeamMembership membership, BaseTeam child, TeamOfTeams parent)
    {
        return new TeamMembershipEdge
        {
            Id = membership.Id,
            StartDate = membership.DateRange.Start,
            EndDate = membership.DateRange.End,
            FromNode = TeamNode.From(child),
            ToNode = TeamNode.From(parent)
        };
    }
}
