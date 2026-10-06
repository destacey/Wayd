using Bogus;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Domain.Tests.Data;

public sealed class WorkItemTrackedStateFaker : Faker<WorkItemTrackedState>
{
    public WorkItemTrackedStateFaker()
    {
        CustomInstantiator(f => new WorkItemTrackedState(
            IterationId: f.Random.Guid(),
            ExternalIterationId: f.Random.Int(1, 10_000),
            StatusId: f.Random.Int(1, 100),
            StatusName: f.PickRandom("New", "Active", "Resolved", "Closed"),
            StatusCategory: f.PickRandom<WorkStatusCategory>(),
            WorkTypeId: f.Random.Int(1, 100),
            WorkTypeName: f.PickRandom("User Story", "Bug"),
            TeamKey: null,
            AssignedToId: f.Random.Guid(),
            AssignedToExternalId: f.Random.Guid().ToString(),
            StoryPoints: f.Random.Int(1, 13),
            Effort: f.Random.Int(1, 40),
            Size: f.Random.Int(1, 8)));
    }
}

public static class WorkItemTrackedStateFakerExtensions
{
    public static WorkItemTrackedStateFaker WithStatusName(this WorkItemTrackedStateFaker faker, string statusName)
    {
        faker.RuleFor(x => x.StatusName, statusName);
        return faker;
    }

    public static WorkItemTrackedStateFaker WithExternalIterationId(this WorkItemTrackedStateFaker faker, int? externalIterationId)
    {
        faker.RuleFor(x => x.ExternalIterationId, externalIterationId);
        return faker;
    }
}
