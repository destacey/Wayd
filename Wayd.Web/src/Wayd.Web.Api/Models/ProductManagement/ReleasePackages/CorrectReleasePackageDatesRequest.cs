namespace Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

/// <summary>
/// Corrects a package's recorded target date and released moment.
/// </summary>
/// <remarks>
/// Both are sent, so an omitted target date is cleared. The released moment can be changed on a released
/// package but not cleared, and cannot be added to one that has not been released.
/// </remarks>
public sealed record CorrectReleasePackageDatesRequest
{
    public LocalDate? TargetDate { get; set; }
    public Instant? ReleasedAt { get; set; }
}
