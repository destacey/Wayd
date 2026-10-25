using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Notifications;

/// <summary>Seeds the employees notification tests point at.</summary>
internal static class NotificationTestData
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 24, 10, 0);

    /// <summary>Saves a new active employee with a unique number and email.</summary>
    public static async Task<Employee> SeedEmployee(SqlServerDbContextFixture fixture, bool isActive = true)
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
            isActive: isActive,
            employeeType: null,
            EventActor.System,
            Timestamp);

        await using var context = fixture.CreateContext();
        context.Employees.Add(employee);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return employee;
    }
}
