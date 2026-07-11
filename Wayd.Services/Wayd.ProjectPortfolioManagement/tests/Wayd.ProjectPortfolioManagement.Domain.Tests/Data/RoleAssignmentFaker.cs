using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

/// <summary>
/// Faker for RoleAssignment. Since it's generic, you'll need to specify the role enum type when using it.
/// </summary>
/// <typeparam name="T">The role enum type (e.g., ProjectRole, ProgramRole, ProjectPortfolioRole)</typeparam>
public sealed class RoleAssignmentFaker<T> : PrivateConstructorFaker<RoleAssignment<T>> where T : Enum
{
    public RoleAssignmentFaker()
    {
        RuleFor(x => x.ObjectId, f => f.Random.Guid());
        RuleFor(x => x.Role, f => f.PickRandom<T>());
        RuleFor(x => x.EmployeeId, f => f.Random.Guid());
    }
}

public static class RoleAssignmentFakerExtensions
{
    public static RoleAssignmentFaker<T> WithObjectId<T>(this RoleAssignmentFaker<T> faker, Guid objectId) where T : Enum
    {
        faker.RuleFor(x => x.ObjectId, objectId);
        return faker;
    }

    public static RoleAssignmentFaker<T> WithRole<T>(this RoleAssignmentFaker<T> faker, T role) where T : Enum
    {
        faker.RuleFor(x => x.Role, role);
        return faker;
    }

    public static RoleAssignmentFaker<T> WithEmployeeId<T>(this RoleAssignmentFaker<T> faker, Guid employeeId) where T : Enum
    {
        faker.RuleFor(x => x.EmployeeId, employeeId);
        return faker;
    }
}
