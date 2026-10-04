using System.Reflection;
using Wayd.Tests.Shared.Data;
using Wayd.Tests.Shared.Extensions;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkProcessSchemeFaker : PrivateConstructorFaker<WorkProcessScheme>
{
    private readonly WorkType? _workType;
    private readonly Workflow? _workflow;

    public WorkProcessSchemeFaker()
    {
        _workType = new WorkTypeFaker().Generate();
        _workflow = new WorkflowFaker().Generate();

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.WorkProcessId, f => f.Random.Guid());
        RuleFor(x => x.IsActive, true);

        // Set navigation properties after construction using stored references
        FinishWith((f, scheme) =>
        {
            // The ids must match the navigations: the external work item sync keys its status
            // lookup on WorkTypeId, so a default id makes every item miss it.
            if (_workType != null)
            {
                typeof(WorkProcessScheme).GetProperty("WorkType")!.SetValue(scheme, _workType);
                typeof(WorkProcessScheme)
                    .GetField("<WorkTypeId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(scheme, _workType.Id);
            }
            if (_workflow != null)
            {
                typeof(WorkProcessScheme).GetProperty("Workflow")!.SetValue(scheme, _workflow);
                typeof(WorkProcessScheme).GetProperty("WorkflowId")!.SetValue(scheme, _workflow.Id);
            }
        });
    }
}

public static class WorkProcessSchemeFakerExtensions
{
    public static WorkProcessSchemeFaker WithWorkType(this WorkProcessSchemeFaker faker, WorkType workType)
    {
        faker.SetPrivateField("_workType", workType);
        return faker;
    }

    public static WorkProcessSchemeFaker WithWorkflow(this WorkProcessSchemeFaker faker, Workflow workflow)
    {
        faker.SetPrivateField("_workflow", workflow);
        return faker;
    }

    public static WorkProcessSchemeFaker WithIsActive(this WorkProcessSchemeFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
