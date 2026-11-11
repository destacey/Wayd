using Wayd.Common.Domain.Enums;

namespace Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;

/// <summary>
/// A PPM project as a module that follows its progress copies it: who it is and where it stands in its
/// lifecycle.
/// </summary>
/// <remarks>
/// The lifecycle category, not the status: statuses are on their way to being workflow-configurable, while
/// the category is what <c>ProjectStatusChangedEventV2</c> carries for consumers outside PPM to branch on.
/// </remarks>
public interface ISimpleProjectWithLifecycle : ISimpleProject
{
    /// <summary>Where the project's current status stands in its lifecycle.</summary>
    LifecycleCategory LifecycleCategory { get; }
}
