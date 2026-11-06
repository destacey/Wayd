namespace Wayd.DemandManagement.Application.IntakeRequestTags;

/// <summary>
/// The failures free intake request tag commands share.
/// </summary>
public static class IntakeRequestTagErrors
{
    /// <summary>
    /// The typed name is a tag in more than one category the desk offers, so which was meant is unclear.
    /// </summary>
    public const string AmbiguousName = "More than one tag category has a tag with this name. Choose it from its category.";

    /// <summary>
    /// The tag a merge moves requests to cannot take them: it is inactive, or in an inactive category.
    /// </summary>
    public const string InactiveMergeTarget = "Tags can only be merged into an active tag in an active category.";

    /// <summary>A save refused because a tag or what carries it changed after the checks passed.</summary>
    public const string ChangedWhileSaving = "The tags changed while saving. Reload and try again.";
}
