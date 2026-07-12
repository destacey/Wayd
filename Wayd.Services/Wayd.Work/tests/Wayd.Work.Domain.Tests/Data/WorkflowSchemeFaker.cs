using Wayd.Common.Domain.Enums.Work;
using Wayd.Tests.Shared.Data;
using Wayd.Tests.Shared.Extensions;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkflowSchemeFaker : PrivateConstructorFaker<WorkflowScheme>
{
    private readonly WorkStatus? _workStatus;
    private readonly WorkStatusCategory? _workStatusCategory;

    public WorkflowSchemeFaker()
    {
        _workStatus = new WorkStatusFaker().Generate();
        _workStatusCategory = WorkStatusCategory.Proposed;

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.WorkflowId, f => f.Random.Guid());
        RuleFor(x => x.Order, f => f.Random.Int(1, 10));
        RuleFor(x => x.IsActive, true);

        // Set navigation properties after construction
        FinishWith((f, scheme) =>
        {
            if (_workStatus != null)
            {
                typeof(WorkflowScheme).GetProperty("WorkStatus")!.SetValue(scheme, _workStatus);
            }
            if (_workStatusCategory.HasValue)
            {
                typeof(WorkflowScheme).GetProperty("WorkStatusCategory")!.SetValue(scheme, _workStatusCategory.Value);
            }
        });
    }
}

public static class WorkflowSchemeFakerExtensions
{
    public static WorkflowSchemeFaker WithWorkStatus(this WorkflowSchemeFaker faker, WorkStatus workStatus)
    {
        faker.SetPrivateField("_workStatus", workStatus);
        return faker;
    }

    public static WorkflowSchemeFaker WithWorkStatusCategory(this WorkflowSchemeFaker faker, WorkStatusCategory category)
    {
        faker.SetPrivateField("_workStatusCategory", category);
        return faker;
    }

    public static WorkflowSchemeFaker WithOrder(this WorkflowSchemeFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);
        return faker;
    }

    public static WorkflowSchemeFaker WithIsActive(this WorkflowSchemeFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
