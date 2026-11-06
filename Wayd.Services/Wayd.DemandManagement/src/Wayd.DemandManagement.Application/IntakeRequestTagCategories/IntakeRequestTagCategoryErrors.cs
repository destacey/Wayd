namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories;

/// <summary>
/// The failures tag category commands share, compared by the endpoints to choose a status code.
/// </summary>
public static class IntakeRequestTagCategoryErrors
{
    /// <summary>No tag category has the id given.</summary>
    public const string NotFoundError = "Tag category not found.";

    /// <summary>No tag has the id given, in any category.</summary>
    public const string TagNotFoundError = "Tag not found.";

    /// <summary>Another category, active or not, already has the name.</summary>
    public const string DuplicateNameError = "A tag category with this name already exists.";

    /// <summary>A save refused because the category or what refers to it changed after the checks passed.</summary>
    public const string ChangedWhileSavingError = "The tag category changed while saving. Reload and try again.";
}
