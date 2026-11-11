using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;

namespace Wayd.DemandManagement.Domain.Tests.Data;

/// <summary>
/// A PPM project as its source reports it, for building and resyncing a <c>DemandProject</c>.
/// </summary>
public sealed record SourceProject(Guid Id, ProjectKey Key, string Name, string Description = "desc",
    LifecycleCategory LifecycleCategory = LifecycleCategory.NotStarted) : ISimpleProjectWithLifecycle
{
    public static SourceProject Any(string key = "ATLAS", string name = "Atlas",
        LifecycleCategory lifecycle = LifecycleCategory.NotStarted) =>
        new(Guid.CreateVersion7(), new ProjectKey(key), name, LifecycleCategory: lifecycle);
}
