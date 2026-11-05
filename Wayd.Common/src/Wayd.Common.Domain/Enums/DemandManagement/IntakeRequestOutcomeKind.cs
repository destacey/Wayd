using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.DemandManagement;

/// <summary>
/// What an accepted intake request became, by the kind of record its outcome points at.
/// </summary>
/// <remarks>
/// Declining, deferring and marking a duplicate are statuses, not outcomes: an outcome is always something
/// that delivers the ask.
/// </remarks>
public enum IntakeRequestOutcomeKind
{
    /// <summary>
    /// A project that exists because of the request.
    /// </summary>
    [Display(Name = "New Project", Description = "A project started to deliver this request.", Order = 1)]
    NewProject = 1,

    /// <summary>
    /// An existing project whose scope absorbed the request. Recording it never changes the project.
    /// </summary>
    [Display(Name = "Existing Project Scope", Description = "Scope added to a project that already existed.", Order = 2)]
    ExistingProjectScope = 2,

    /// <summary>
    /// A work item that delivers the request. Recording it never changes the work item.
    /// </summary>
    [Display(Name = "Work Item", Description = "A work item that delivers this request.", Order = 3)]
    WorkItem = 3,
}
