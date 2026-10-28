namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequestTagCategories;

/// <summary>
/// Whether requests can be given a tag.
/// </summary>
public sealed record SetIntakeRequestTagActiveRequest
{
    /// <summary>
    /// True to put the tag back into use, false to take it out of use. Requests already carrying it keep it
    /// either way.
    /// </summary>
    public bool IsActive { get; set; }
}
