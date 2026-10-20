namespace Wayd.Common.Domain.StatusWorkflows.Enums;

/// <summary>
/// Whether a workflow lets a record move between any two of its statuses, or only along the
/// transitions it declares.
/// </summary>
/// <remarks>
/// Explicit rather than inferred from an empty transition set, so removing the last transition from a
/// restricted workflow leaves it refusing every move instead of silently reopening all of them.
/// </remarks>
public enum TransitionMode
{
    /// <summary>
    /// Any status may move to any other. The default, and how every workflow behaved before transitions
    /// existed. Zero so an event payload written before the mode existed reads as this.
    /// </summary>
    AnyToAny = 0,

    /// <summary>A record may only make the moves the workflow's transitions allow.</summary>
    Restricted = 1,
}
