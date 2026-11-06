namespace Wayd.Common.Domain.Tagging;

/// <summary>
/// The failures every module's tag categories and tagged records share. Each module re-exposes these on its own
/// types, so callers compare against the module they work in.
/// </summary>
public static class TagErrors
{
    /// <summary>Changing what a system category means.</summary>
    public const string SystemCategory = "System tag categories cannot be changed.";

    /// <summary>A tag id that is not in the category.</summary>
    public const string TagNotFound = "That tag does not belong to this category.";

    /// <summary>A tag with a blank name.</summary>
    public const string TagNameRequired = "A tag must have a name.";

    /// <summary>A tag name another tag in the category already has.</summary>
    public const string DuplicateTagName = "A tag with this name already exists in this category.";

    /// <summary>Applying a tag, or a tag in a category, that has been deactivated.</summary>
    public const string InactiveTag = "An inactive tag cannot be applied.";

    /// <summary>A tag name longer than <see cref="TagBase.NameMaxLength"/>.</summary>
    public const string TagNameTooLong = "A tag name can be at most 64 characters.";

    /// <summary>Changing a tag in a category as if it were free; it changes through its category.</summary>
    public const string NotAFreeTag = "Only a free tag can be changed on its own. A tag in a category changes through its category.";

    /// <summary>Merging a tag into itself.</summary>
    public const string MergeIntoItself = "A tag cannot be merged into itself.";

    /// <summary>A free tag name another free tag in the module already has.</summary>
    public const string DuplicateFreeTagName = "A free tag with this name already exists.";
}
