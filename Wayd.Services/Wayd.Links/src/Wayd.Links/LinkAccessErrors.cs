namespace Wayd.Links;

/// <summary>
/// The failures link commands and queries report when the caller may not do something, beside the domain's own rules
/// in <c>LinkErrors</c>.
/// </summary>
/// <remarks>
/// An endpoint maps these by value: <see cref="NotFound"/> to 404, <see cref="CannotManage"/> to 403, and anything
/// else to 400.
/// </remarks>
public static class LinkAccessErrors
{
    /// <summary>
    /// The record or the link does not exist, or the caller cannot read the record. Reported as 404 either way, so a
    /// link never confirms a record the caller cannot see.
    /// </summary>
    public const string NotFound = "Link or record not found.";

    /// <summary>Changing a record's links without being allowed to edit the record.</summary>
    public const string CannotManage = "You cannot change this record's links.";

    /// <summary>Whether a failure is <see cref="NotFound"/>.</summary>
    public static bool IsNotFound(string? error) => error == NotFound;

    /// <summary>Whether a failure means the caller may not do this.</summary>
    public static bool IsForbidden(string? error) => error == CannotManage;
}
