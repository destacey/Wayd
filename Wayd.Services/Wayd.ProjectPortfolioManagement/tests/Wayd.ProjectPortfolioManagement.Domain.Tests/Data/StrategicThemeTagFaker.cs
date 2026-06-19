using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

/// <summary>
/// Faker for StrategicThemeTag. Since it's generic, you'll need to specify the object type when using it.
/// </summary>
/// <typeparam name="T">The object type (e.g., Project, Program, StrategicInitiative)</typeparam>
public sealed class StrategicThemeTagFaker<T> : PrivateConstructorFaker<StrategicThemeTag<T>> where T : class
{
    public StrategicThemeTagFaker()
    {
        RuleFor(x => x.ObjectId, f => f.Random.Guid());
        RuleFor(x => x.StrategicThemeId, f => f.Random.Guid());
    }
}

public static class StrategicThemeTagFakerExtensions
{
    public static StrategicThemeTagFaker<T> WithObjectId<T>(this StrategicThemeTagFaker<T> faker, Guid objectId) where T : class
    {
        faker.RuleFor(x => x.ObjectId, objectId);
        return faker;
    }

    public static StrategicThemeTagFaker<T> WithStrategicThemeId<T>(this StrategicThemeTagFaker<T> faker, Guid strategicThemeId) where T : class
    {
        faker.RuleFor(x => x.StrategicThemeId, strategicThemeId);
        return faker;
    }
}
