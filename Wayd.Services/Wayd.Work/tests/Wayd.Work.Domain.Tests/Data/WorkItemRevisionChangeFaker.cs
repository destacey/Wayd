using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkItemRevisionChangeFaker : PrivateConstructorFaker<WorkItemRevisionChange>
{
    public WorkItemRevisionChangeFaker()
    {
        RuleFor(x => x.WorkItemRevisionId, f => f.Random.Guid());
        RuleFor(x => x.FieldName, f => f.Lorem.Word());
        RuleFor(x => x.OldValue, f => f.Lorem.Word());
        RuleFor(x => x.NewValue, f => f.Lorem.Word());
    }
}

public static class WorkItemRevisionChangeFakerExtensions
{
    public static WorkItemRevisionChangeFaker WithWorkItemRevisionId(this WorkItemRevisionChangeFaker faker, Guid workItemRevisionId)
    {
        faker.RuleFor(x => x.WorkItemRevisionId, workItemRevisionId);
        return faker;
    }

    public static WorkItemRevisionChangeFaker WithFieldName(this WorkItemRevisionChangeFaker faker, string fieldName)
    {
        faker.RuleFor(x => x.FieldName, fieldName);
        return faker;
    }

    public static WorkItemRevisionChangeFaker WithOldValue(this WorkItemRevisionChangeFaker faker, string? oldValue)
    {
        faker.RuleFor(x => x.OldValue, oldValue);
        return faker;
    }

    public static WorkItemRevisionChangeFaker WithNewValue(this WorkItemRevisionChangeFaker faker, string? newValue)
    {
        faker.RuleFor(x => x.NewValue, newValue);
        return faker;
    }
}
