namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// A team invited to edit a story map. The team is stored, not its members, so access follows the team's current
/// membership: its members and the members of its current team of teams.
/// </summary>
public sealed class StoryMapEditorTeam
{
    private StoryMapEditorTeam() { }

    internal StoryMapEditorTeam(Guid storyMapId, Guid teamId)
    {
        StoryMapId = storyMapId;
        TeamId = teamId;
    }

    /// <summary>The map the team edits.</summary>
    public Guid StoryMapId { get; private init; }

    /// <summary>The editing team or team of teams.</summary>
    public Guid TeamId { get; private init; }

    /// <summary>The editing team, when loaded.</summary>
    public PlanningTeam? Team { get; private init; }
}
