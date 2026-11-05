using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Planning.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Seeds the employees and teams that Planning records with people on them (poker sessions, story maps) point to.
/// </summary>
internal static class PlanningTestData
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

    /// <summary>Saves a new active team copy in Planning, with a unique key and code.</summary>
    public static async Task<PlanningTeam> SeedTeam(SqlServerDbContextFixture fixture, CancellationToken ct)
    {
        var team = new PlanningTeam(new SeedableTeam(), Timestamp);

        await using var context = fixture.CreateContext();
        context.PlanningTeams.Add(team);
        await context.SaveChangesAsync(ct);

        return team;
    }

    private sealed class SeedableTeam : ISimpleTeam
    {
        public Guid Id { get; } = Guid.CreateVersion7();
        public int Key { get; } = Random.Shared.Next(1_000_000, int.MaxValue);
        public string Name => $"Team {Code.Value}";
        public TeamCode Code { get; } = new($"T{Guid.NewGuid():N}"[..10].ToUpperInvariant());
        public TeamType Type => TeamType.Team;
        public bool IsActive => true;
    }
}
