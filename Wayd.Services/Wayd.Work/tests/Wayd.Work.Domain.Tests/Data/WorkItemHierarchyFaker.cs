using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;
using NodaTime;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkItemHierarchyFaker : PrivateConstructorFaker<WorkItemHierarchy>
{
    public WorkItemHierarchyFaker(Instant? timestamp = null)
    {
        var ts = timestamp ?? SystemClock.Instance.GetCurrentInstant();

        RuleFor(x => x.SourceId, f => f.Random.Guid());
        RuleFor(x => x.TargetId, f => f.Random.Guid());
        RuleFor(x => x.CreatedOn, ts);
        RuleFor(x => x.CreatedById, f => f.Random.Guid());
        RuleFor(x => x.RemovedOn, (Instant?)null);
        RuleFor(x => x.RemovedById, (Guid?)null);
        RuleFor(x => x.Comment, f => f.Lorem.Sentence());
    }
}

public static class WorkItemHierarchyFakerExtensions
{
    public static WorkItemHierarchyFaker WithSourceId(this WorkItemHierarchyFaker faker, Guid sourceId)
    {
        faker.RuleFor(x => x.SourceId, sourceId);
        return faker;
    }

    public static WorkItemHierarchyFaker WithTargetId(this WorkItemHierarchyFaker faker, Guid targetId)
    {
        faker.RuleFor(x => x.TargetId, targetId);
        return faker;
    }

    public static WorkItemHierarchyFaker WithCreatedOn(this WorkItemHierarchyFaker faker, Instant createdOn)
    {
        faker.RuleFor(x => x.CreatedOn, createdOn);
        return faker;
    }

    public static WorkItemHierarchyFaker WithCreatedById(this WorkItemHierarchyFaker faker, Guid? createdById)
    {
        faker.RuleFor(x => x.CreatedById, createdById);
        return faker;
    }

    public static WorkItemHierarchyFaker WithRemovedOn(this WorkItemHierarchyFaker faker, Instant? removedOn)
    {
        faker.RuleFor(x => x.RemovedOn, removedOn);
        return faker;
    }

    public static WorkItemHierarchyFaker WithRemovedById(this WorkItemHierarchyFaker faker, Guid? removedById)
    {
        faker.RuleFor(x => x.RemovedById, removedById);
        return faker;
    }

    public static WorkItemHierarchyFaker WithComment(this WorkItemHierarchyFaker faker, string? comment)
    {
        faker.RuleFor(x => x.Comment, comment);
        return faker;
    }
}
