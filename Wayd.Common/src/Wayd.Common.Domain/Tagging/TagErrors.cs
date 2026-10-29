namespace Wayd.Common.Domain.Tagging;

/// <summary>
/// The failures every module's tag axes and tagged records share. Each module re-exposes these on its own types,
/// so callers compare against the module they work in.
/// </summary>
public static class TagErrors
{
    /// <summary>Changing what a system axis means.</summary>
    public const string SystemAxis = "System tag axes cannot be changed.";

    /// <summary>A tag id that is not on the axis.</summary>
    public const string TagNotFound = "That tag does not belong to this axis.";

    /// <summary>A tag with a blank name.</summary>
    public const string TagNameRequired = "A tag must have a name.";

    /// <summary>A tag name another tag on the axis already has.</summary>
    public const string DuplicateTagName = "A tag with this name already exists on this axis.";

    /// <summary>Applying a tag, or a tag on an axis, that has been deactivated.</summary>
    public const string InactiveTag = "An inactive tag cannot be applied.";

    /// <summary>A tag name longer than <see cref="TagBase.NameMaxLength"/>.</summary>
    public const string TagNameTooLong = "A tag name can be at most 64 characters.";

    /// <summary>Changing a tag on an axis as if it were free; it changes through its axis.</summary>
    public const string NotAFreeTag = "Only a free tag can be changed on its own. A tag on an axis changes through its axis.";

    /// <summary>Merging a tag into itself.</summary>
    public const string MergeIntoItself = "A tag cannot be merged into itself.";

    /// <summary>A free tag name another free tag in the module already has.</summary>
    public const string DuplicateFreeTagName = "A free tag with this name already exists.";
}
