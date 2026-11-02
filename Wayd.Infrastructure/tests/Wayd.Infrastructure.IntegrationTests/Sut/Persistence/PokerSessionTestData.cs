using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

internal static class PokerSessionTestData
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 1, 15, 9, 0);

    /// <summary>Saves a new active employee with a unique number and email.</summary>
    public static async Task<Employee> SeedEmployee(SqlServerDbContextFixture fixture, CancellationToken ct)
    {
        var suffix = $"{Guid.CreateVersion7():N}"[..12];
        var employee = Employee.Create(
            new PersonName("Avery", null, "Chen"),
            suffix,
            Timestamp,
            new EmailAddress($"{suffix}@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            Timestamp);

        await using var context = fixture.CreateContext();
        context.Employees.Add(employee);
        await context.SaveChangesAsync(ct);

        return employee;
    }
}
