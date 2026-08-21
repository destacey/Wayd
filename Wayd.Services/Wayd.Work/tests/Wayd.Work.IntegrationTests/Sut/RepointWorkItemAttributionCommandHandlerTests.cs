using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler uses a set-based <c>ExecuteUpdate</c> whose
/// predicate reaches through the <c>ExtendedProps</c> reference navigation. Whether EF can
/// translate that is exactly the risk, and no in-memory fake can answer it — the fakes execute
/// LINQ against lists, so a query that fails to translate in production passes there.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class RepointWorkItemAttributionCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string AssignedExternalId = "6f2a0c94-e5b8-4d17-9a63-2c8e1b74f052";
    private const string OtherExternalId = "8d1e5c70-3a84-4b29-9f61-2c7e0a53d918";

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private RepointWorkItemAttributionCommandHandler CreateHandler(WaydDbContextAccessor accessor) =>
        new(accessor.Context, Mock.Of<ILogger<RepointWorkItemAttributionCommandHandler>>());

    [Fact]
    public async Task Handle_RepointsItemsCarryingTheExternalIdentity()
    {
        // Arrange
        await _fixture.ResetWorkData(TestContext.Current.CancellationToken);
        var seeded = await WorkItemSeeder.Seed(_fixture, AssignedExternalId, OtherExternalId,
            TestContext.Current.CancellationToken);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = CreateHandler(accessor);

        // Act
        var result = await handler.Handle(
            new RepointWorkItemAttributionCommand(AssignedExternalId, seeded.EmployeeId),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verify = new WaydDbContextAccessor(_fixture);
        var matched = await verify.Context.WorkItems
            .AsNoTracking()
            .FirstAsync(w => w.Id == seeded.MatchingWorkItemId, TestContext.Current.CancellationToken);
        // All three, not just the assignee: a mapping repairs the whole record. Reporting on
        // "created by" would stay wrong otherwise, with the data to fix it sitting right there.
        matched.AssignedToId.Should().Be(seeded.EmployeeId);
        matched.CreatedById.Should().Be(seeded.EmployeeId);
        matched.LastModifiedById.Should().Be(seeded.EmployeeId);
    }

    [Fact]
    public async Task Handle_LeavesItemsCarryingADifferentIdentityAlone()
    {
        // Arrange
        await _fixture.ResetWorkData(TestContext.Current.CancellationToken);
        var seeded = await WorkItemSeeder.Seed(_fixture, AssignedExternalId, OtherExternalId,
            TestContext.Current.CancellationToken);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = CreateHandler(accessor);

        // Act
        await handler.Handle(
            new RepointWorkItemAttributionCommand(AssignedExternalId, seeded.EmployeeId),
            TestContext.Current.CancellationToken);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var other = await verify.Context.WorkItems
            .AsNoTracking()
            .FirstAsync(w => w.Id == seeded.OtherWorkItemId, TestContext.Current.CancellationToken);
        other.AssignedToId.Should().BeNull();
        other.CreatedById.Should().BeNull();
        other.LastModifiedById.Should().BeNull();
    }

    // What an admin means by ignoring an identity that had been matched to the wrong person.
    [Fact]
    public async Task Handle_ClearsTheAttribution_WhenNoEmployeeIsGiven()
    {
        // Arrange
        await _fixture.ResetWorkData(TestContext.Current.CancellationToken);
        var seeded = await WorkItemSeeder.Seed(_fixture, AssignedExternalId, OtherExternalId,
            TestContext.Current.CancellationToken);

        await using var seedAccessor = new WaydDbContextAccessor(_fixture);
        await CreateHandler(seedAccessor).Handle(
            new RepointWorkItemAttributionCommand(AssignedExternalId, seeded.EmployeeId),
            TestContext.Current.CancellationToken);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = CreateHandler(accessor);

        // Act
        var result = await handler.Handle(
            new RepointWorkItemAttributionCommand(AssignedExternalId, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verify = new WaydDbContextAccessor(_fixture);
        var matched = await verify.Context.WorkItems
            .AsNoTracking()
            .FirstAsync(w => w.Id == seeded.MatchingWorkItemId, TestContext.Current.CancellationToken);
        matched.AssignedToId.Should().BeNull();
        matched.CreatedById.Should().BeNull();
        matched.LastModifiedById.Should().BeNull();
    }

    [Fact]
    public async Task Handle_FailsWhenNoExternalIdentityIsGiven()
    {
        // Arrange
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = CreateHandler(accessor);

        // Act
        var result = await handler.Handle(
            new RepointWorkItemAttributionCommand("   ", Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
