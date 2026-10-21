namespace Wayd.ProductManagement.Application.DeliveryWorkflows;

/// <summary>
/// A lifecycle action on a version, release or release package that moves its status.
/// </summary>
/// <remarks>
/// Each one moves the record to the status a well-known alias names, so whether a record's workflow
/// allows it can be answered without trying it. Which records support which action is the record's own
/// matter: only a version is cut, and a package is never reverted.
/// </remarks>
public enum DeliveryAction
{
    /// <summary>Freezes a version's scope, moving it to <c>Ready</c>. Versions only.</summary>
    Cut = 1,

    /// <summary>Records that the record shipped or was announced, moving it to <c>Released</c>.</summary>
    MarkReleased = 2,

    /// <summary>
    /// Records that a release never in fact happened, moving it back — to <c>Ready</c>, or for a version
    /// never cut, to its workflow's initial status. Versions and releases.
    /// </summary>
    RevertRelease = 3,

    /// <summary>Pulls or retracts the record, moving it to <c>Withdrawn</c>.</summary>
    Withdraw = 4,
}
