using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared.Data;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;
using Wayd.TestData.Core;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProjectStageFaker : PrivateConstructorFaker<ProjectStage>
{
    public ProjectStageFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ProjectId, f => f.Random.Guid());
        RuleFor(x => x.ProjectLifecycleStageId, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.Status, f => TaskStatus.NotStarted);
        RuleFor(x => x.Order, f => f.Random.Int(1, 10));
        RuleFor(x => x.Progress, f => Progress.NotStarted());
    }
}

public static class ProjectStageFakerExtensions
{
    public static ProjectStageFaker WithId(this ProjectStageFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ProjectStageFaker WithProjectId(this ProjectStageFaker faker, Guid projectId)
    {
        faker.RuleFor(x => x.ProjectId, projectId);

        return faker;
    }

    public static ProjectStageFaker WithProjectLifecycleStageId(this ProjectStageFaker faker, Guid projectLifecycleStageId)
    {
        faker.RuleFor(x => x.ProjectLifecycleStageId, projectLifecycleStageId);

        return faker;
    }

    public static ProjectStageFaker WithName(this ProjectStageFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static ProjectStageFaker WithDescription(this ProjectStageFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static ProjectStageFaker WithStatus(this ProjectStageFaker faker, TaskStatus status)
    {
        faker.RuleFor(x => x.Status, status);

        return faker;
    }

    public static ProjectStageFaker WithOrder(this ProjectStageFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }

    public static ProjectStageFaker WithProgress(this ProjectStageFaker faker, Progress? progress)
    {
        faker.RuleFor(x => x.Progress, progress);

        return faker;
    }
}
