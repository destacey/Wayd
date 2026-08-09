namespace Wayd.Common.Domain.Enums;

/// <summary>
/// Represents the high-level position of an item in its lifecycle.
/// This is descriptive metadata only and does not imply success, failure,
/// or the reason the work ended.
/// </summary>
public enum LifecyclePhase
{
    NotStarted,
    Active,
    Done
}
