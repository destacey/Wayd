namespace Wayd.Common.Domain.Enums;

/// <summary>
/// Represents the high-level position of an item in its lifecycle.
/// This is descriptive metadata only and does not imply success, failure,
/// or the reason the work ended.
/// These are unordered categories: the numeric values are stable identifiers.
/// Do not infer lifecycle direction by comparing them.
/// </summary>
public enum LifecycleCategory
{
    NotStarted = 0,
    Active = 1,
    Completed = 2,
    Canceled = 3
}
