using NodaTime;
using NodaTime.Extensions;
using Wayd.Tests.Shared.Data;
using Wayd.Tests.Shared.Extensions;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkItemDependencyFaker : PrivateConstructorFaker<WorkItemDependency>
{
    public WorkItemDependencyFaker(Instant now)
    {
        var workItemFaker = new WorkItemFaker();
        var sourceWorkItem = workItemFaker.WithProposedState().Generate();
        var targetWorkItem = workItemFaker.WithProposedState().Generate();

        RuleFor(x => x.SourceId, sourceWorkItem.Id);
        RuleFor(x => x.Source, sourceWorkItem);
        RuleFor(x => x.SourceStatusCategory, sourceWorkItem.StatusCategory);
        RuleFor(x => x.TargetId, targetWorkItem.Id);
        RuleFor(x => x.Target, targetWorkItem);
        RuleFor(x => x.TargetStatusCategory, targetWorkItem.StatusCategory);
        RuleFor(x => x.SourcePlannedOn, f => null);
        RuleFor(x => x.TargetPlannedOn, f => null);
        RuleFor(x => x.CreatedOn, f => f.Date.Past().ToInstant());
        RuleFor(x => x.CreatedById, f => f.Random.Guid());
        RuleFor(x => x.Comment, f => f.Lorem.Sentence());


        // Call CalculateStateAndHealth() after generation
        this.FinishWith("CalculateStateAndHealth", now);
    }
}

public static class WorkItemDependencyFakerExtensions
{
    public static WorkItemDependencyFaker WithSource(this WorkItemDependencyFaker faker, WorkItem source)
    {
        faker.RuleFor(x => x.SourceId, source.Id);
        faker.RuleFor(x => x.Source, source);
        faker.RuleFor(x => x.SourceStatusCategory, source.StatusCategory);

        return faker;
    }

    public static WorkItemDependencyFaker WithTarget(this WorkItemDependencyFaker faker, WorkItem target)
    {
        faker.RuleFor(x => x.TargetId, target.Id);
        faker.RuleFor(x => x.Target, target);
        faker.RuleFor(x => x.TargetStatusCategory, target.StatusCategory);

        return faker;
    }

    public static WorkItemDependencyFaker WithSourcePlannedOn(this WorkItemDependencyFaker faker, Instant? sourcePlannedOn)
    {
        faker.RuleFor(x => x.SourcePlannedOn, sourcePlannedOn);

        return faker;
    }

    public static WorkItemDependencyFaker WithTargetPlannedOn(this WorkItemDependencyFaker faker, Instant? targetPlannedOn)
    {
        faker.RuleFor(x => x.TargetPlannedOn, targetPlannedOn);

        return faker;
    }

    public static WorkItemDependencyFaker WithCreatedOn(this WorkItemDependencyFaker faker, Instant createdOn)
    {
        faker.RuleFor(x => x.CreatedOn, createdOn);

        return faker;
    }

    public static WorkItemDependencyFaker WithCreatedById(this WorkItemDependencyFaker faker, Guid? createdById)
    {
        faker.RuleFor(x => x.CreatedById, createdById);

        return faker;
    }

    public static WorkItemDependencyFaker WithComment(this WorkItemDependencyFaker faker, string? comment)
    {
        faker.RuleFor(x => x.Comment, comment);

        return faker;
    }
}
