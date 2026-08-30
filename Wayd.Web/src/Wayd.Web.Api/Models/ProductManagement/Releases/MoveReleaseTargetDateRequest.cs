namespace Wayd.Web.Api.Models.ProductManagement.Releases;

/// <summary>
/// Moves or clears a release's target date.
/// </summary>
public sealed record MoveReleaseTargetDateRequest
{
    /// <summary>
    /// The new target date, or null to clear it.
    /// </summary>
    public LocalDate? TargetDate { get; set; }
}
