namespace Wayd.Common.Domain.Enums.ProjectPortfolioManagement;

/// <summary>
/// The well-known meanings a project deliverable's status can carry, whatever a company's workflow calls it.
/// </summary>
/// <remarks>
/// Rules bind to these, never to a status name or id: a deliverable starts in NotStarted, an intake request
/// counts as met when its deliverables are Accepted, and Descoped is the deliberate way to close one without
/// delivering it. Values are persisted on every deliverable and status transition — never renumber one — and
/// <c>0</c> is reserved to match <c>StatusWorkflow.NoAlias</c>.
/// </remarks>
public enum ProjectDeliverableStatusAlias
{
    /// <summary>No well-known meaning: a stage a company added, such as In Review.</summary>
    None = 0,

    /// <summary>Committed to, with no work begun. Where every deliverable starts.</summary>
    NotStarted = 1,

    /// <summary>Being worked on.</summary>
    InProgress = 2,

    /// <summary>Delivered and accepted against its acceptance criteria.</summary>
    Accepted = 3,

    /// <summary>Deliberately dropped from the project's scope, with a reason.</summary>
    Descoped = 4,
}
