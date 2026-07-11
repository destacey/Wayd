using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;
using NodaTime;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkTypeHierarchyFaker : PrivateConstructorFaker<WorkTypeHierarchy>
{
    public WorkTypeHierarchyFaker(Instant? timestamp = null)
    {
        var ts = timestamp ?? SystemClock.Instance.GetCurrentInstant();

        RuleFor(x => x.Id, 1); // WorkTypeHierarchy typically has Id = 1 as it's a singleton

        // WorkTypeHierarchy uses Initialize() static method which sets up the levels
        // We need to use FinishWith to call Initialize and replace the instance
        FinishWith((f, hierarchy) =>
        {
            // The Initialize method already sets up the default levels
            // If we need to customize, we can add portfolio levels after generation
        });
    }
}

public static class WorkTypeHierarchyFakerExtensions
{
    public static WorkTypeHierarchyFaker WithTimestamp(this WorkTypeHierarchyFaker faker, Instant timestamp)
    {
        // Since WorkTypeHierarchy.Initialize is called during construction,
        // we need to use CustomInstantiator for this specific case
        // as the timestamp is used in the static factory method
        faker.CustomInstantiator(f => WorkTypeHierarchy.Initialize(timestamp));
        return faker;
    }
}
