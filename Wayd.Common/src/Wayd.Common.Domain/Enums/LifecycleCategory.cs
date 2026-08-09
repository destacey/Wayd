namespace Wayd.Common.Domain.Enums;

/// <summary>
/// Represents the high-level position of an item in its lifecycle.
/// This is descriptive metadata only and does not imply success, failure,
/// or the reason the work ended.
/// Members are ordered NotStarted -> Active -> Completed; the relative order is
/// significant and must not be changed. Canceled sits outside that progression
/// as a terminal off-ramp and must not be treated as further along than Completed.
/// </summary>
public enum LifecycleCategory
{
    NotStarted = 0,
    Active = 1,
    Completed = 2,
    Canceled = 3
}
