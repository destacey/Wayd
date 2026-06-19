using Wayd.Common.Domain.Employees;
using Wayd.Common.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class EmployeeFaker : PrivateConstructorFaker<Employee>
{
    public EmployeeFaker()
    {
        var hireDate = SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromDays(FakerHub.Random.Int(30, 1095)));

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1000, 10000));
        RuleFor(x => x.Name, f => new PersonName(f.Name.FirstName(), null, f.Name.LastName()));
        RuleFor(x => x.EmployeeNumber, f => f.Random.AlphaNumeric(8));
        RuleFor(x => x.HireDate, hireDate);
        RuleFor(x => x.Email, f => new EmailAddress(f.Internet.Email()));
        RuleFor(x => x.JobTitle, f => f.Name.JobTitle());
        RuleFor(x => x.Department, f => f.Commerce.Department());
        RuleFor(x => x.OfficeLocation, f => f.Address.City());
        RuleFor(x => x.ManagerId, (Guid?)null);
        RuleFor(x => x.IsActive, true);
    }
}

public static class EmployeeFakerExtensions
{
    public static EmployeeFaker WithId(this EmployeeFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static EmployeeFaker WithKey(this EmployeeFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static EmployeeFaker WithName(this EmployeeFaker faker, PersonName name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static EmployeeFaker WithEmployeeNumber(this EmployeeFaker faker, string employeeNumber)
    {
        faker.RuleFor(x => x.EmployeeNumber, employeeNumber);
        return faker;
    }

    public static EmployeeFaker WithHireDate(this EmployeeFaker faker, Instant? hireDate)
    {
        faker.RuleFor(x => x.HireDate, hireDate);
        return faker;
    }

    public static EmployeeFaker WithEmail(this EmployeeFaker faker, EmailAddress email)
    {
        faker.RuleFor(x => x.Email, email);
        return faker;
    }

    public static EmployeeFaker WithJobTitle(this EmployeeFaker faker, string? jobTitle)
    {
        faker.RuleFor(x => x.JobTitle, jobTitle);
        return faker;
    }

    public static EmployeeFaker WithDepartment(this EmployeeFaker faker, string? department)
    {
        faker.RuleFor(x => x.Department, department);
        return faker;
    }

    public static EmployeeFaker WithOfficeLocation(this EmployeeFaker faker, string? officeLocation)
    {
        faker.RuleFor(x => x.OfficeLocation, officeLocation);
        return faker;
    }

    public static EmployeeFaker WithManagerId(this EmployeeFaker faker, Guid? managerId)
    {
        faker.RuleFor(x => x.ManagerId, managerId);
        return faker;
    }

    public static EmployeeFaker WithIsActive(this EmployeeFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }

    public static EmployeeFaker AsActive(this EmployeeFaker faker)
    {
        faker.RuleFor(x => x.IsActive, true);
        return faker;
    }

    public static EmployeeFaker AsInactive(this EmployeeFaker faker)
    {
        faker.RuleFor(x => x.IsActive, false);
        return faker;
    }

    public static EmployeeFaker AsManager(this EmployeeFaker faker)
    {
        faker.RuleFor(x => x.ManagerId, (Guid?)null);
        return faker;
    }

    public static EmployeeFaker WithManager(this EmployeeFaker faker, Guid managerId)
    {
        faker.RuleFor(x => x.ManagerId, managerId);
        return faker;
    }
}
