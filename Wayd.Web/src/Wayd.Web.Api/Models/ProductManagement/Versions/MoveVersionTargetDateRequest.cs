namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// Moves or clears a version's target date.
/// </summary>
public sealed record MoveVersionTargetDateRequest
{
    /// <summary>
    /// The new target date, or null to clear it.
    /// </summary>
    public LocalDate? TargetDate { get; set; }
}
