using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class StrategicInitiativeProjectFaker : PrivateConstructorFaker<StrategicInitiativeProject>
{
    public StrategicInitiativeProjectFaker()
    {
        RuleFor(x => x.StrategicInitiativeId, f => f.Random.Guid());
        RuleFor(x => x.ProjectId, f => f.Random.Guid());
    }
}

public static class StrategicInitiativeProjectFakerExtensions
{
    public static StrategicInitiativeProjectFaker WithStrategicInitiativeId(this StrategicInitiativeProjectFaker faker, Guid strategicInitiativeId)
    {
        faker.RuleFor(x => x.StrategicInitiativeId, strategicInitiativeId);
        return faker;
    }

    public static StrategicInitiativeProjectFaker WithProjectId(this StrategicInitiativeProjectFaker faker, Guid projectId)
    {
        faker.RuleFor(x => x.ProjectId, projectId);
        return faker;
    }
}
