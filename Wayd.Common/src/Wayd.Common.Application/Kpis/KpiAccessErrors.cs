namespace Wayd.Common.Application.Kpis;

/// <summary>
/// The failures KPI commands and queries report when the caller may not do something, or collided with someone else,
/// beside the domain's own rules in <c>KpiErrors</c>.
/// </summary>
/// <remarks>
/// An endpoint maps these by value: <see cref="NotFound"/> to 404, <see cref="CannotManage"/> and
/// <see cref="CannotManageCategories"/> to 403, <see cref="Conflict"/> to 409, and anything else to 400.
/// </remarks>
public static class KpiAccessErrors
{
    /// <summary>
    /// The record, the KPI, the measurement or the category being changed does not exist, or the caller cannot read the record.
    /// Reported as 404 either way, so a KPI never confirms a record the caller cannot see.
    /// </summary>
    public const string NotFound = "KPI or record not found.";

    /// <summary>Changing a record's KPIs without being allowed to.</summary>
    public const string CannotManage = "You cannot change this record's KPIs.";

    /// <summary>Changing the organization's KPI categories without the KPI administrator permission.</summary>
    public const string CannotManageCategories = "Only a KPI administrator can change KPI categories.";

    /// <summary>Someone else changed the KPI or the category in the moment between reading and saving.</summary>
    public const string Conflict = "Someone else changed this at the same moment. Reload it and try again.";

    /// <summary>A category name another category already has.</summary>
    public const string DuplicateCategoryName = "Another KPI category already has this name.";

    /// <summary>A category id, given for a KPI, that no category has. A 400, unlike <see cref="NotFound"/>.</summary>
    public const string CategoryNotFound = "This KPI category does not exist. Choose another.";

    /// <summary>A category that is not active, given for a KPI.</summary>
    public const string CategoryInactive = "This KPI category is inactive. Choose an active one.";

    /// <summary>Deleting a category KPIs use.</summary>
    public const string CategoryInUse = "KPIs use this category, so it cannot be deleted. Deactivate it instead.";

    /// <summary>Whether a failure is <see cref="NotFound"/>.</summary>
    public static bool IsNotFound(string? error) => error == NotFound;

    /// <summary>Whether a failure means the caller may not do this.</summary>
    public static bool IsForbidden(string? error) => error is CannotManage or CannotManageCategories;

    /// <summary>Whether a failure is <see cref="Conflict"/>.</summary>
    public static bool IsConflict(string? error) => error == Conflict;
}
