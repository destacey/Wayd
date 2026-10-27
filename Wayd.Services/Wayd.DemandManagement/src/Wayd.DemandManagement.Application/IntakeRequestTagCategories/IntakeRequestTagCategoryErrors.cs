namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories;

/// <summary>
/// The failures tag axis commands share, compared by the endpoints to choose a status code.
/// </summary>
public static class IntakeRequestTagCategoryErrors
{
    /// <summary>No tag axis has the id given.</summary>
    public const string NotFoundError = "Tag axis not found.";

    /// <summary>No tag has the id given, on any axis.</summary>
    public const string TagNotFoundError = "Tag not found.";

    /// <summary>Another axis, active or not, already has the name.</summary>
    public const string DuplicateNameError = "A tag axis with this name already exists.";
}
