using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Models;
using Wayd.Infrastructure.Following;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Following;

/// <summary>
/// The store against real SQL Server: what makes following idempotent is the table's key, which no fake
/// enforces.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class FollowStoreIntegrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.follow-store.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 23, 9, 30);

    [Fact]
    public async Task Add_ShouldSaveTheFollow()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var employee = await SeedEmployee();
        var ownerId = Guid.CreateVersion7();
        await using var context = fixture.CreateContext();

        // Act
        var added = await new FollowStore(context).Add(NewFollow(ownerId, employee.Id), cancellationToken);

        // Assert
        added.Should().BeTrue();
        await using var reader = fixture.CreateContext();
        var stored = await reader.Follows.SingleAsync(f => f.OwnerId == ownerId, cancellationToken);
        stored.OwnerType.Should().Be(GadgetType);
        stored.EmployeeId.Should().Be(employee.Id);
        stored.FollowedOn.Should().Be(Timestamp);
    }

    [Fact]
    public async Task Add_ShouldReportAlreadyFollowing_AndKeepTheFirstFollow()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var employee = await SeedEmployee();
        var ownerId = Guid.CreateVersion7();
        await using (var first = fixture.CreateContext())
        {
            await new FollowStore(first).Add(NewFollow(ownerId, employee.Id), cancellationToken);
        }

        await using var context = fixture.CreateContext();

        // Act
        var added = await new FollowStore(context).Add(
            NewFollow(ownerId, employee.Id, Timestamp.Plus(Duration.FromDays(1))), cancellationToken);

        // Assert
        added.Should().BeFalse();
        await using var reader = fixture.CreateContext();
        var stored = await reader.Follows.Where(f => f.OwnerId == ownerId).ToListAsync(cancellationToken);
        stored.Should().ContainSingle().Which.FollowedOn.Should().Be(Timestamp);
    }

    [Fact]
    public async Task Add_ShouldLetExactlyOneOfManyConcurrentFollowsWin()
    {
        // Arrange — each caller on its own context, as concurrent requests would be, so every one can pass
        // the existence check before any of them commits.
        var cancellationToken = TestContext.Current.CancellationToken;
        var employee = await SeedEmployee();
        var ownerId = Guid.CreateVersion7();
        var contexts = Enumerable.Range(0, 8).Select(_ => fixture.CreateContext()).ToList();

        try
        {
            // Act
            var outcomes = await Task.WhenAll(contexts.Select(c =>
                Task.Run(() => new FollowStore(c).Add(NewFollow(ownerId, employee.Id), cancellationToken), cancellationToken)));

            // Assert
            outcomes.Count(added => added).Should().Be(1);
            await using var reader = fixture.CreateContext();
            (await reader.Follows.CountAsync(f => f.OwnerId == ownerId, cancellationToken)).Should().Be(1);
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Add_ShouldKeepSeparateFollowsForSeparatePeopleAndRecords()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var avery = await SeedEmployee();
        var blake = await SeedEmployee();
        var gadgetA = Guid.CreateVersion7();
        var gadgetB = Guid.CreateVersion7();
        await using var context = fixture.CreateContext();
        var store = new FollowStore(context);

        // Act
        var outcomes = new[]
        {
            await store.Add(NewFollow(gadgetA, avery.Id), cancellationToken),
            await store.Add(NewFollow(gadgetA, blake.Id), cancellationToken),
            await store.Add(NewFollow(gadgetB, avery.Id), cancellationToken),
        };

        // Assert
        outcomes.Should().AllSatisfy(added => added.Should().BeTrue());
        await using var reader = fixture.CreateContext();
        (await reader.Follows.CountAsync(f => f.OwnerId == gadgetA || f.OwnerId == gadgetB, cancellationToken)).Should().Be(3);
    }

    [Fact]
    public async Task Remove_ShouldDeleteOnlyThatPersonsFollowOfThatRecord()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var avery = await SeedEmployee();
        var blake = await SeedEmployee();
        var gadgetA = Guid.CreateVersion7();
        var gadgetB = Guid.CreateVersion7();
        await using (var seed = fixture.CreateContext())
        {
            var seeder = new FollowStore(seed);
            await seeder.Add(NewFollow(gadgetA, avery.Id), cancellationToken);
            await seeder.Add(NewFollow(gadgetA, blake.Id), cancellationToken);
            await seeder.Add(NewFollow(gadgetB, avery.Id), cancellationToken);
        }

        await using var context = fixture.CreateContext();

        // Act
        var removed = await new FollowStore(context).Remove(GadgetType, gadgetA, avery.Id, cancellationToken);

        // Assert
        removed.Should().BeTrue();
        await using var reader = fixture.CreateContext();
        var remaining = await reader.Follows
            .Where(f => f.OwnerId == gadgetA || f.OwnerId == gadgetB)
            .Select(f => new { f.OwnerId, f.EmployeeId })
            .ToListAsync(cancellationToken);
        remaining.Should().BeEquivalentTo([
            new { OwnerId = gadgetA, EmployeeId = blake.Id },
            new { OwnerId = gadgetB, EmployeeId = avery.Id },
        ]);
    }

    [Fact]
    public async Task Remove_ShouldReportNothingRemoved_WhenThereWasNoFollow()
    {
        // Arrange
        var employee = await SeedEmployee();
        await using var context = fixture.CreateContext();

        // Act
        var removed = await new FollowStore(context).Remove(GadgetType, Guid.CreateVersion7(), employee.Id, TestContext.Current.CancellationToken);

        // Assert
        removed.Should().BeFalse();
    }

    private static Follow NewFollow(Guid ownerId, Guid employeeId, Instant? followedOn = null)
    {
        FollowOwners.Register(Gadget);
        return Follow.Create(GadgetType, ownerId, employeeId, followedOn ?? Timestamp).Value;
    }

    private async Task<Employee> SeedEmployee()
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

        await using WaydDbContext context = fixture.CreateContext();
        context.Employees.Add(employee);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return employee;
    }
}
