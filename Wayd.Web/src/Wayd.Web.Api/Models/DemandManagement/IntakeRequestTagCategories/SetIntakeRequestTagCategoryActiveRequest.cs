namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequestTagCategories;

/// <summary>
/// Whether requests can be tagged along a tag axis.
/// </summary>
public sealed record SetIntakeRequestTagCategoryActiveRequest
{
    /// <summary>
    /// True to put the axis back into use, false to take it out of use. Requests already tagged along it keep
    /// their tags either way.
    /// </summary>
    public bool IsActive { get; set; }
}
