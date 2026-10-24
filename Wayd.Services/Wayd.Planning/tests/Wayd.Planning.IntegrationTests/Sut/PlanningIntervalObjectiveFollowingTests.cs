using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Domain;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Proves a soft-deleted objective leaves its followers' list and count against SQL Server.
/// </summary>
/// <remarks>
/// Objectives are soft-deleted, and the read rule relies on the context's soft-delete query filter to drop them.
/// An in-memory fake has no query filter, so only a real provider shows the deleted objective is gone — and
/// would catch a read rule that bypassed the filter.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class PlanningIntervalObjectiveFollowingTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task ASoftDeletedObjective_LeavesTheFollowingListAndItsCount()
    {
        // Arrange — two followed objectives on one interval, one then deleted through the real command
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;
        PlanningFollowOwners.Register();
        var ownerType = PlanningFollowOwners.PlanningIntervalObjective.Key;

        Guid intervalId;
        int intervalKey;
        Guid keptId;
        Guid deletedId;
        Guid employeeId;
        await using (var seed = _fixture.CreateContext())
        {
            var team = await PlanningSeed.Team(seed, ct);

            var interval = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
                new LocalDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 3, 1)), 4, "Iteration ", EventActor.System, now).Value;
            interval.ManageTeams([team.Id], EventActor.System, now);
            var kept = interval.CreateObjective(team, "Ship the thing", null, false, null, null, 1, EventActor.System, now).Value;
            var deleted = interval.CreateObjective(team, "Drop the thing", null, false, null, null, 2, EventActor.System, now).Value;
            seed.PlanningIntervals.Add(interval);

            var suffix = $"{Guid.CreateVersion7():N}"[..12];
            var employee = Employee.Create(new PersonName("Avery", null, "Chen"), suffix, now,
                new EmailAddress($"{suffix}@acme.example"), jobTitle: null, department: null, officeLocation: null,
                managerId: null, isActive: true, employeeType: null, EventActor.System, now);
            seed.Employees.Add(employee);
            await seed.SaveChangesAsync(ct);

            seed.Follows.Add(Follow.Create(ownerType, kept.Id, employee.Id, now).Value);
            seed.Follows.Add(Follow.Create(ownerType, deleted.Id, employee.Id, now.Plus(Duration.FromMinutes(1))).Value);
            await seed.SaveChangesAsync(ct);

            intervalId = interval.Id;
            intervalKey = interval.Key;
            keptId = kept.Id;
            deletedId = deleted.Id;
            employeeId = employee.Id;
        }

        await using (var delete = _fixture.CreateContext())
        {
            var handler = new DeletePlanningIntervalObjectiveCommandHandler(delete, SqlServerDbContextFixture.CurrentUser(),
                SqlServerDbContextFixture.DateTimeProvider(now.Plus(Duration.FromHours(1))), NullLogger<DeletePlanningIntervalObjectiveCommandHandler>.Instance);
            var deleted = await handler.Handle(new DeletePlanningIntervalObjectiveCommand(intervalId, deletedId), ct);
            deleted.IsSuccess.Should().BeTrue(deleted.IsFailure ? deleted.Error : null);
        }

        await using var context = _fixture.CreateContext();
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        var readRule = new PlanningIntervalObjectiveReadRule(context, principal.Object);
        var handlerUnderTest = new GetMyFollowsQueryHandler(context, [readRule], principal.Object);

        // Act
        var result = await handlerUnderTest.Handle(new GetMyFollowsQuery(ownerType), ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var follows = result.Value.Should().ContainSingle().Subject.Follows;
        follows.TotalCount.Should().Be(1);
        var item = follows.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(keptId);
        item.Name.Should().Be("Ship the thing");
        item.ParentKey.Should().Be(intervalKey.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
