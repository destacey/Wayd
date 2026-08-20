using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProjectLifecycleFaker : PrivateConstructorFaker<ProjectLifecycle>
{
    public ProjectLifecycleFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.State, f => ProjectLifecycleState.Proposed);
    }
}

public static class ProjectLifecycleFakerExtensions
{
    public static ProjectLifecycleFaker WithId(this ProjectLifecycleFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ProjectLifecycleFaker WithKey(this ProjectLifecycleFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static ProjectLifecycleFaker WithName(this ProjectLifecycleFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static ProjectLifecycleFaker WithDescription(this ProjectLifecycleFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static ProjectLifecycleFaker WithState(this ProjectLifecycleFaker faker, ProjectLifecycleState state)
    {
        faker.RuleFor(x => x.State, state);

        return faker;
    }

    /// <summary>
    /// Generates a proposed lifecycle with the specified stages.
    /// </summary>
    public static ProjectLifecycle AsProposedWithStages(this ProjectLifecycleFaker faker, params (string Name, string Description)[] stages)
    {
        var lifecycle = faker.Generate();
        foreach (var (name, description) in stages)
        {
            lifecycle.AddStage(name, description);
        }
        return lifecycle;
    }

    /// <summary>
    /// Generates an active lifecycle with the specified stages.
    /// </summary>
    public static ProjectLifecycle AsActiveWithStages(this ProjectLifecycleFaker faker, params (string Name, string Description)[] stages)
    {
        var lifecycle = faker.AsProposedWithStages(stages);
        lifecycle.Activate();
        return lifecycle;
    }

    /// <summary>
    /// Adds stages to an existing lifecycle using the lifecycle's AddStage method.
    /// </summary>
    public static ProjectLifecycle WithStages(this ProjectLifecycle lifecycle, params (string Name, string Description)[] stages)
    {
        foreach (var (name, description) in stages)
        {
            lifecycle.AddStage(name, description);
        }
        return lifecycle;
    }
}
