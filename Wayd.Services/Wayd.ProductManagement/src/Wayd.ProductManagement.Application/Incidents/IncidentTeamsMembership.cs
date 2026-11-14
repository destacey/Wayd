namespace Wayd.ProductManagement.Application.Incidents;

/// <summary>
/// Decides whether a change to who reported an incident, or who commands it, should add those
/// people to the chat Wayd created. The connector cannot add members yet, so that invite is skipped.
/// </summary>
public static class IncidentTeamsMembership
{
    /// <summary>Said when an invite was due and the connector cannot add members.</summary>
    public const string MembersNotAdded =
        "The Teams connector cannot add members to an incident chat yet.";

    /// <summary>The Teams connector has no operation that adds members to an existing chat.</summary>
    public static bool ConnectorCanAddMembers => false;

    /// <summary>
    /// True when Wayd created the chat, a watched person changed, and the connector can add them.
    /// Who submitted the incident cannot change, so that person is not a reason to invite.
    /// </summary>
    public static bool ShouldInvite(bool createdByWayd, bool reportedByChanged, bool commanderChanged) =>
        createdByWayd && ConnectorCanAddMembers && (reportedByChanged || commanderChanged);

    /// <summary>
    /// True when an invite would have been made and was skipped because members cannot be added.
    /// </summary>
    public static bool SkippedBecauseMembersCannotBeAdded(
        bool createdByWayd, bool reportedByChanged, bool commanderChanged) =>
        createdByWayd && !ConnectorCanAddMembers && (reportedByChanged || commanderChanged);
}
