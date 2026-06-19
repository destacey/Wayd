using Wayd.Common.Domain.Employees;
using Wayd.Common.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

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
