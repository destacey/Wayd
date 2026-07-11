using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Tests.Shared.Data;
using Wayd.Tests.Shared.Extensions;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkflowFaker : PrivateConstructorFaker<Workflow>
{
    private readonly WorkStatus? _workStatus = null;
    private readonly WorkStatusCategory? _workStatusCategory = null;

    public WorkflowFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Random.Words(2));
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.Ownership, Ownership.Owned);
        RuleFor(x => x.IsActive, true);
        RuleFor("_schemes", f => new List<WorkflowScheme>());

        // Set up the workflow scheme if configured
        FinishWith((f, workflow) =>
        {
            if (_workStatus != null && _workStatusCategory.HasValue)
            {
                var scheme = new WorkflowSchemeFaker()
                    .WithWorkStatus(_workStatus)
                    .WithWorkStatusCategory(_workStatusCategory.Value)
                    .Generate();

                // Set the bidirectional relationship
                typeof(WorkflowScheme).GetProperty("Workflow")!.SetValue(scheme, workflow);

                var schemesList = GenericExtensions.GetPrivateList<WorkflowScheme>(workflow, "_schemes");
                schemesList.Clear();
                schemesList.Add(scheme);
            }
        });
    }
}

public static class WorkflowFakerExtensions
{
    public static WorkflowFaker WithId(this WorkflowFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static WorkflowFaker WithName(this WorkflowFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static WorkflowFaker WithSchemes(this WorkflowFaker faker, List<WorkflowScheme> schemes)
    {
        faker.RuleFor("_schemes", f => schemes);
        return faker;
    }

    public static WorkflowFaker WithWorkflowScheme(this WorkflowFaker faker, WorkStatus workStatus, WorkStatusCategory category)
    {
        // Store the work status and category in the faker instance using the helper
        faker.SetPrivateField("_workStatus", workStatus);
        faker.SetPrivateField("_workStatusCategory", category);

        return faker;
    }

    public static WorkflowFaker WithIsActive(this WorkflowFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
