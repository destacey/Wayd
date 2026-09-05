namespace Wayd.Organization.Domain.Models;

public sealed class TeamMembership : BaseMembership
{
    private TeamMembership() { }

    private TeamMembership(BaseTeam source, TeamOfTeams target, MembershipDateRange dateRange)
    {
        if (source.Id == target.Id)
        {
            throw new ArgumentException("A team or team of teams cannot have a membership with its self.");
        }

        SourceId = source.Id;
        TargetId = target.Id;
        Source = source;
        Target = target;
        DateRange = dateRange;
    }

    /// <summary>Gets the source or child team or team of teams.</summary>
    /// <value>The source.</value>
    public BaseTeam Source { get; private set; } = default!;

    /// <summary>Gets the target or parent team of teams.</summary>
    /// <value>The target.</value>
    public TeamOfTeams Target { get; private set; } = default!;

    /// <summary>
    /// Both navigations are set here rather than left to EF. A membership added in memory is not known to
    /// the context until it saves, so relationship fixup cannot populate them — and the hierarchy checks
    /// walk <see cref="Source"/> to recurse, so without this they are blind to any edge added in the same
    /// batch as the one being validated.
    /// </summary>
    internal static TeamMembership Create(BaseTeam child, TeamOfTeams parent, MembershipDateRange dateRange)
    {
        return new TeamMembership(child, parent, dateRange);
    }
}
