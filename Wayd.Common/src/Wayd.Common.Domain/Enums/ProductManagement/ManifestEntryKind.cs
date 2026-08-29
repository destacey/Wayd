using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.ProductManagement;

/// <summary>
/// Whether a component in a release package's manifest actually changed in that package, or was
/// carried forward at the version it was already running.
/// </summary>
/// <remarks>
/// This distinction is the reason a manifest is worth recording at all. A weekly package where four of
/// fifteen services changed still has to state what the other eleven were running, or "what was in
/// production on this date" cannot be answered from the manifest alone. Without the kind, a reader
/// cannot tell a service that shipped a change from one that merely came along.
/// </remarks>
public enum ManifestEntryKind
{
    [Display(Name = "Changed", Description = "The component shipped a new version in this package.", Order = 1)]
    Changed = 1,

    [Display(Name = "Carried Forward", Description = "The component was unchanged; its existing version is recorded for completeness.", Order = 2)]
    CarriedForward = 2
}
