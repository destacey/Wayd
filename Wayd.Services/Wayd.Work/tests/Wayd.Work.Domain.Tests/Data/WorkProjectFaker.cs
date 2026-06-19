using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkProjectFaker : PrivateConstructorFaker<WorkProject>
{
    public WorkProjectFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => new ProjectKey(f.Random.AlphaNumeric(5)));
        RuleFor(x => x.Name, f => f.Company.CompanyName());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
    }
}

public static class WorkProjectFakerExtensions
{
    public static WorkProjectFaker WithId(this WorkProjectFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static WorkProjectFaker WithKey(this WorkProjectFaker faker, ProjectKey key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static WorkProjectFaker WithName(this WorkProjectFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static WorkProjectFaker WithDescription(this WorkProjectFaker faker, string description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }
}
