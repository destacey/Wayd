using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared.Data;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;
using Wayd.TestData.Core;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProjectPhaseFaker : PrivateConstructorFaker<ProjectPhase>
{
    public ProjectPhaseFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ProjectId, f => f.Random.Guid());
        RuleFor(x => x.ProjectLifecyclePhaseId, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.Status, f => TaskStatus.NotStarted);
        RuleFor(x => x.Order, f => f.Random.Int(1, 10));
        RuleFor(x => x.Progress, f => Progress.NotStarted());
    }
}

public static class ProjectPhaseFakerExtensions
{
    public static ProjectPhaseFaker WithId(this ProjectPhaseFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ProjectPhaseFaker WithProjectId(this ProjectPhaseFaker faker, Guid projectId)
    {
        faker.RuleFor(x => x.ProjectId, projectId);

        return faker;
    }

    public static ProjectPhaseFaker WithProjectLifecyclePhaseId(this ProjectPhaseFaker faker, Guid projectLifecyclePhaseId)
    {
        faker.RuleFor(x => x.ProjectLifecyclePhaseId, projectLifecyclePhaseId);

        return faker;
    }

    public static ProjectPhaseFaker WithName(this ProjectPhaseFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static ProjectPhaseFaker WithDescription(this ProjectPhaseFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static ProjectPhaseFaker WithStatus(this ProjectPhaseFaker faker, TaskStatus status)
    {
        faker.RuleFor(x => x.Status, status);

        return faker;
    }

    public static ProjectPhaseFaker WithOrder(this ProjectPhaseFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }

    public static ProjectPhaseFaker WithProgress(this ProjectPhaseFaker faker, Progress? progress)
    {
        faker.RuleFor(x => x.Progress, progress);

        return faker;
    }
}
