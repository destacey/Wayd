namespace Wayd.ProductManagement.Application.ProductTags;

/// <summary>
/// The failures free product tag commands share.
/// </summary>
public static class ProductTagErrors
{
    /// <summary>No tag has the id given.</summary>
    public const string NotFound = "Tag not found.";

    /// <summary>Free tags are turned off for products.</summary>
    public const string FreeTagsTurnedOff = "Free tags are turned off for products. Choose a tag from one of the tag categories.";

    /// <summary>The typed name is a tag on more than one axis, so which was meant is unclear.</summary>
    public const string AmbiguousName = "More than one tag category has a tag with this name. Choose it from its category.";

    /// <summary>The tag a merge moves products to cannot take them: it is inactive, or on an inactive axis.</summary>
    public const string InactiveMergeTarget = "Tags can only be merged into an active tag on an active category.";

    /// <summary>A save refused because a tag or what carries it changed after the checks passed.</summary>
    public const string ChangedWhileSaving = "The tags changed while saving. Reload and try again.";
}
