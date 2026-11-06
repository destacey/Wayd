namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequestTagCategories;

/// <summary>
/// Whether requests can be tagged in a tag category.
/// </summary>
public sealed record SetIntakeRequestTagCategoryActiveRequest
{
    /// <summary>
    /// True to put the category back into use, false to take it out of use. Requests already tagged in it keep
    /// their tags either way.
    /// </summary>
    public bool IsActive { get; set; }
}
