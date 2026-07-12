using Wayd.Tests.Shared.Data;
using Wayd.Tests.Shared.Extensions;
using Wayd.Work.Domain.Models;
using NodaTime;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkItemRevisionFaker : PrivateConstructorFaker<WorkItemRevision>
{
    public WorkItemRevisionFaker(Instant? timestamp = null)
    {
        var ts = timestamp ?? SystemClock.Instance.GetCurrentInstant();

        RuleFor(x => x.WorkItemId, f => f.Random.Guid());
        RuleFor(x => x.Revision, f => f.Random.Int(1, 100));
        RuleFor(x => x.RevisedById, f => f.Random.Guid());
        RuleFor(x => x.RevisedDate, ts);

        // Set up the changes collection using FinishWith
        FinishWith((f, revision) =>
        {
            var changes = new WorkItemRevisionChangeFaker()
                .WithWorkItemRevisionId(revision.WorkItemId)
                .Generate(f.Random.Int(1, 5));

            var changesList = GenericExtensions.GetPrivateList<WorkItemRevisionChange>(revision, "_changes");
            changesList.Clear();
            changesList.AddRange(changes);
        });
    }
}

public static class WorkItemRevisionFakerExtensions
{
    public static WorkItemRevisionFaker WithWorkItemId(this WorkItemRevisionFaker faker, Guid workItemId)
    {
        faker.RuleFor(x => x.WorkItemId, workItemId);
        return faker;
    }

    public static WorkItemRevisionFaker WithRevisionNumber(this WorkItemRevisionFaker faker, int revision)
    {
        faker.RuleFor(x => x.Revision, revision);
        return faker;
    }

    public static WorkItemRevisionFaker WithRevisedById(this WorkItemRevisionFaker faker, Guid? revisedById)
    {
        faker.RuleFor(x => x.RevisedById, revisedById);
        return faker;
    }

    public static WorkItemRevisionFaker WithRevisedDate(this WorkItemRevisionFaker faker, Instant revisedDate)
    {
        faker.RuleFor(x => x.RevisedDate, revisedDate);
        return faker;
    }

    public static WorkItemRevisionFaker WithChanges(this WorkItemRevisionFaker faker, IEnumerable<WorkItemRevisionChange> changes)
    {
        faker.FinishWith((f, revision) =>
        {
            var changesList = GenericExtensions.GetPrivateList<WorkItemRevisionChange>(revision, "_changes");
            changesList.Clear();
            changesList.AddRange(changes);
        });
        return faker;
    }
}
