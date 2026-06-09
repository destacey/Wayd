using FluentAssertions.Extensions;
using NodaTime;
using NodaTime.Extensions;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkItemFaker : PrivateConstructorFaker<WorkItem>
{
    public WorkItemFaker(Guid? workspaceId = null)
    {
        var workType = new WorkTypeFaker().Generate();
        var workStatus = new WorkStatusFaker().Generate();

        var workspaceIdValue = workspaceId ?? FakerHub.Random.Guid();
        var category = FakerHub.Random.Enum<WorkStatusCategory>();

        Instant created = FakerHub.Date.Past().AsUtc().ToInstant();
        Instant? activatedTimestamp = null;
        Instant? doneTimestamp = null;

        var randomDays = FakerHub.Random.Int(0, 5);

        if (category is WorkStatusCategory.Active)
        {
            activatedTimestamp = created.Plus(Duration.FromDays(randomDays));
        }
        else if (category is WorkStatusCategory.Done)
        {
            activatedTimestamp = created.Plus(Duration.FromDays(randomDays));
            doneTimestamp = activatedTimestamp.Value.Plus(Duration.FromDays(FakerHub.Random.Int(1, 10)));
        }
        else if (category is WorkStatusCategory.Removed)
        {
            if (randomDays > 0)
            {
                activatedTimestamp = created.Plus(Duration.FromDays(randomDays));
                doneTimestamp = activatedTimestamp.Value.Plus(Duration.FromDays(FakerHub.Random.Int(1, 10)));
            }
            else
            {
                activatedTimestamp = doneTimestamp = created.Plus(Duration.FromDays(FakerHub.Random.Int(1, 10)));
            }
        }

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.WorkspaceId, f => workspaceIdValue);
        RuleFor(x => x.ExternalId, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Title, f => f.Random.String2(10));
        RuleFor(x => x.TypeId, workType.Id);
        RuleFor(x => x.Type, workType);
        RuleFor(x => x.StatusId, workStatus.Id);
        RuleFor(x => x.Status, workStatus);
        RuleFor(x => x.StatusCategory, category);

        RuleFor(x => x.ParentId, f => null);
        RuleFor(x => x.Parent, f => null);
        RuleFor(x => x.Priority, f => f.Random.Int(1, 4));
        RuleFor(x => x.StackRank, f => f.Random.Double(1000, 100000));
        RuleFor(x => x.StoryPoints, f => f.Random.Double(0, 20));

        RuleFor(x => x.ProjectId, f => null);
        RuleFor(x => x.ParentProjectId, f => null);

        RuleFor(x => x.IterationId, f => null);

        RuleFor(x => x.Created, created);
        RuleFor(x => x.CreatedById, f => f.Random.Guid());
        RuleFor(x => x.LastModified, f => f.Date.Recent().AsUtc().ToInstant());
        RuleFor(x => x.LastModifiedById, f => f.Random.Guid());
        RuleFor(x => x.AssignedToId, f => f.Random.Guid());

        RuleFor(x => x.ActivatedTimestamp, activatedTimestamp);
        RuleFor(x => x.DoneTimestamp, doneTimestamp);
        RuleFor("_tags", f => new List<WorkItemTag>());
    }
}

public static class WorkItemFakerExtensions
{
    public static WorkItemFaker WithWorkspaceId(this WorkItemFaker faker, Guid workspaceId)
    {
        faker.RuleFor(x => x.WorkspaceId, workspaceId);

        return faker;
    }

    public static WorkItemFaker WithTitle(this WorkItemFaker faker, string? title)
    {
        faker.RuleFor(x => x.Title, title);

        return faker;
    }

    public static WorkItemFaker WithType(this WorkItemFaker faker, WorkType type)
    {
        faker.RuleFor(x => x.TypeId, type.Id);
        faker.RuleFor(x => x.Type, type);

        return faker;
    }

    public static WorkItemFaker WithStatus(this WorkItemFaker faker, WorkStatus status)
    {
        faker.RuleFor(x => x.StatusId, status.Id);
        faker.RuleFor(x => x.Status, status);

        return faker;
    }

    public static WorkItemFaker WithStatusCategory(this WorkItemFaker faker, WorkStatusCategory statusCategory)
    {
        faker.RuleFor(x => x.StatusCategory, statusCategory);

        return faker;
    }

    public static WorkItemFaker WithParentId(this WorkItemFaker faker, Guid? parentId)
    {
        faker.RuleFor(x => x.ParentId, parentId);

        return faker;
    }

    public static WorkItemFaker WithCreated(this WorkItemFaker faker, Instant created)
    {
        faker.RuleFor(x => x.Created, created);

        return faker;
    }

    public static WorkItemFaker WithCreatedById(this WorkItemFaker faker, Guid? createdById)
    {
        faker.RuleFor(x => x.CreatedById, createdById);

        return faker;
    }

    public static WorkItemFaker WithLastModified(this WorkItemFaker faker, Instant lastModified)
    {
        faker.RuleFor(x => x.LastModified, lastModified);

        return faker;
    }

    public static WorkItemFaker WithLastModifiedById(this WorkItemFaker faker, Guid? lastModifiedById)
    {
        faker.RuleFor(x => x.LastModifiedById, lastModifiedById);

        return faker;
    }

    public static WorkItemFaker WithAssignedToId(this WorkItemFaker faker, Guid? assignedToId)
    {
        faker.RuleFor(x => x.AssignedToId, assignedToId);

        return faker;
    }

    public static WorkItemFaker WithPriority(this WorkItemFaker faker, int? priority)
    {
        faker.RuleFor(x => x.Priority, priority);

        return faker;
    }

    public static WorkItemFaker WithStackRank(this WorkItemFaker faker, double stackRank)
    {
        faker.RuleFor(x => x.StackRank, stackRank);

        return faker;
    }

    public static WorkItemFaker WithProjectId(this WorkItemFaker faker, Guid? projectId)
    {
        faker.RuleFor(x => x.ProjectId, projectId);

        return faker;
    }

    public static WorkItemFaker WithParentProjectId(this WorkItemFaker faker, Guid? parentProjectId)
    {
        faker.RuleFor(x => x.ParentProjectId, parentProjectId);

        return faker;
    }

    public static WorkItemFaker WithIterationId(this WorkItemFaker faker, Guid? iterationId)
    {
        faker.RuleFor(x => x.IterationId, iterationId);

        return faker;
    }

    public static WorkItemFaker WithActivatedTimestamp(this WorkItemFaker faker, Instant? activatedTimestamp)
    {
        faker.RuleFor(x => x.ActivatedTimestamp, activatedTimestamp);

        return faker;
    }

    public static WorkItemFaker WithDoneTimestamp(this WorkItemFaker faker, Instant? doneTimestamp)
    {
        faker.RuleFor(x => x.DoneTimestamp, doneTimestamp);

        return faker;
    }

    /// <summary>
    /// Creates a work item that hasn't started yet (Proposed status category with no activated or done timestamps).
    /// </summary>
    public static WorkItemFaker WithProposedState(this WorkItemFaker faker)
    {
        var workStatus = new WorkStatusFaker().WithName("To Do").Generate();

        faker.RuleFor(x => x.StatusCategory, WorkStatusCategory.Proposed);
        faker.RuleFor(x => x.StatusId, workStatus.Id);
        faker.RuleFor(x => x.Status, workStatus);
        faker.RuleFor(x => x.ActivatedTimestamp, (Instant?)null);
        faker.RuleFor(x => x.DoneTimestamp, (Instant?)null);

        return faker;
    }

    /// <summary>
    /// Creates a work item that is currently in progress (Active status category with activated timestamp set and no done timestamp).
    /// </summary>
    /// <param name="faker"></param>
    /// <returns></returns>
    public static WorkItemFaker WithActiveState(this WorkItemFaker faker)
    {
        var workStatus = new WorkStatusFaker().WithName("In Progress").Generate();
        var created = faker.Generate().Created;
        var activated = created.Plus(Duration.FromDays(1));

        faker.RuleFor(x => x.StatusCategory, WorkStatusCategory.Active);
        faker.RuleFor(x => x.StatusId, workStatus.Id);
        faker.RuleFor(x => x.Status, workStatus);
        faker.RuleFor(x => x.ActivatedTimestamp, activated);
        faker.RuleFor(x => x.DoneTimestamp, (Instant?)null);

        return faker;
    }

    /// <summary>
    /// Creates a work item that has been completed (Done status category with both activated and done timestamps set).
    /// </summary>
    /// <param name="faker"></param>
    /// <returns></returns>
    public static WorkItemFaker WithDoneState(this WorkItemFaker faker)
    {
        var workStatus = new WorkStatusFaker().WithName("Done").Generate();
        var created = faker.Generate().Created;
        var activated = created.Plus(Duration.FromDays(1));
        var done = activated.Plus(Duration.FromDays(3));

        faker.RuleFor(x => x.StatusCategory, WorkStatusCategory.Done);
        faker.RuleFor(x => x.StatusId, workStatus.Id);
        faker.RuleFor(x => x.Status, workStatus);
        faker.RuleFor(x => x.ActivatedTimestamp, activated);
        faker.RuleFor(x => x.DoneTimestamp, done);

        return faker;
    }

    public static WorkItemFaker WithExternalId(this WorkItemFaker faker, int externalId)
    {
        faker.RuleFor(x => x.ExternalId, externalId);
        return faker;
    }

    public static WorkItemFaker WithTags(this WorkItemFaker faker, params string[] tags)
    {
        faker.RuleFor("_tags", f => tags.Select(t => new WorkItemTag(t)).ToList());
        return faker;
    }
}