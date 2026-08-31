using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Domain.Tests.Data;

public sealed class DeploymentFaker : PrivateConstructorFaker<Deployment>
{
    public DeploymentFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.ReleaseId, f => f.Random.Guid());
        RuleFor(x => x.PackageId, f => null);
        RuleFor(x => x.EnvironmentId, f => f.Random.Guid());
        RuleFor(x => x.EnvironmentCategory, f => EnvironmentCategory.Production);
        RuleFor(x => x.ArtifactId, f => null);
        RuleFor(x => x.StartedAt, f => Instant.FromUtc(2026, 5, 1, 9, 0));
        RuleFor(x => x.CompletedAt, f => null);
        RuleFor(x => x.Reason, f => null);
        RuleFor(x => x.StatusId, f => f.Random.Guid());
        // A real record always has one: ApplyStatus sets it from the StatusRef it is given,
        // and building the outgoing side of a transition needs it.
        RuleFor(x => x.StatusWorkflowId, f => f.Random.Guid());
        RuleFor(x => x.StatusName, f => "Seeded Status");
        RuleFor(x => x.StatusCategory, f => StatusCategory.Active);
        RuleFor("StatusAliasValue", f => (int)ProductStatusAlias.InProgress);
    }
}

public static class DeploymentFakerExtensions
{
    public static DeploymentFaker WithId(this DeploymentFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static DeploymentFaker WithKey(this DeploymentFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static DeploymentFaker WithReleaseId(this DeploymentFaker faker, Guid? releaseId)
    {
        faker.RuleFor(x => x.ReleaseId, releaseId);

        return faker;
    }

    public static DeploymentFaker WithPackageId(this DeploymentFaker faker, Guid? packageId)
    {
        faker.RuleFor(x => x.PackageId, packageId);

        return faker;
    }

    public static DeploymentFaker WithEnvironmentId(this DeploymentFaker faker, Guid environmentId)
    {
        faker.RuleFor(x => x.EnvironmentId, environmentId);

        return faker;
    }

    public static DeploymentFaker WithEnvironmentCategory(this DeploymentFaker faker, EnvironmentCategory category)
    {
        faker.RuleFor(x => x.EnvironmentCategory, category);

        return faker;
    }

    public static DeploymentFaker WithArtifactId(this DeploymentFaker faker, string? artifactId)
    {
        faker.RuleFor(x => x.ArtifactId, artifactId);

        return faker;
    }

    public static DeploymentFaker WithStartedAt(this DeploymentFaker faker, Instant startedAt)
    {
        faker.RuleFor(x => x.StartedAt, startedAt);

        return faker;
    }

    public static DeploymentFaker WithCompletedAt(this DeploymentFaker faker, Instant? completedAt)
    {
        faker.RuleFor(x => x.CompletedAt, completedAt);

        return faker;
    }

    public static DeploymentFaker WithReason(this DeploymentFaker faker, string? reason)
    {
        faker.RuleFor(x => x.Reason, reason);

        return faker;
    }

    public static DeploymentFaker WithStatusId(this DeploymentFaker faker, Guid statusId)
    {
        faker.RuleFor(x => x.StatusId, statusId);

        return faker;
    }

    public static DeploymentFaker WithStatusName(this DeploymentFaker faker, string statusName)
    {
        faker.RuleFor(x => x.StatusName, statusName);

        return faker;
    }

    public static DeploymentFaker WithStatusCategory(this DeploymentFaker faker, StatusCategory category)
    {
        faker.RuleFor(x => x.StatusCategory, category);

        return faker;
    }

    public static DeploymentFaker WithOutcome(this DeploymentFaker faker, ProductStatusAlias outcome)
    {
        faker.RuleFor("StatusAliasValue", _ => (int)outcome);

        return faker;
    }

    /// <summary>
    /// A deployment that reached its environment.
    /// </summary>
    public static DeploymentFaker AsSucceeded(this DeploymentFaker faker, Instant completedAt)
    {
        faker.RuleFor(x => x.CompletedAt, completedAt);
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Done);
        faker.RuleFor("StatusAliasValue", _ => (int)ProductStatusAlias.Succeeded);

        return faker;
    }

    /// <summary>
    /// A deployment that never reached its environment.
    /// </summary>
    public static DeploymentFaker AsFailed(this DeploymentFaker faker, Instant completedAt)
    {
        faker.RuleFor(x => x.CompletedAt, completedAt);
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Removed);
        faker.RuleFor("StatusAliasValue", _ => (int)ProductStatusAlias.Failed);

        return faker;
    }

    /// <summary>
    /// A deployment that reached its environment and was then reverted.
    /// </summary>
    public static DeploymentFaker AsRolledBack(this DeploymentFaker faker, Instant rolledBackAt)
    {
        faker.RuleFor(x => x.CompletedAt, rolledBackAt);
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Removed);
        faker.RuleFor("StatusAliasValue", _ => (int)ProductStatusAlias.RolledBack);

        return faker;
    }
}
