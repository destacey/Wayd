namespace Wayd.Common.Application.Following;

/// <summary>
/// A named cut of the caller's follows: the records that need a look for one reason.
/// </summary>
/// <remarks>
/// A closed set the Following list filters and counts on. A new reason to look is a new value here and a new
/// branch in <c>FollowedRows.In</c>; the page lists whatever the counts return.
/// </remarks>
public enum FollowView
{
    /// <summary>Records whose current health is Unhealthy.</summary>
    Unhealthy = 1,

    /// <summary>Records with activity in the last seven days.</summary>
    ChangedRecently = 2,

    /// <summary>Records that have finished, whatever the outcome.</summary>
    Closed = 3,
}

/// <summary>
/// The order the Following list is shown in.
/// </summary>
public enum FollowSort
{
    /// <summary>Most recently changed first; records with no activity last.</summary>
    LastChanged = 1,

    /// <summary>Most recently followed first.</summary>
    Followed = 2,
}
