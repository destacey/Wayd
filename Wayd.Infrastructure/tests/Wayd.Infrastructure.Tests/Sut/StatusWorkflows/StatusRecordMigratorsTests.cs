using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.StatusWorkflows;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Domain;

namespace Wayd.Infrastructure.Tests.Sut.StatusWorkflows;

/// <summary>
/// The guard every record migrator shares: Product Management records carry no scope, so a scoped
/// migration or count is refused rather than widened to every record on the workflow.
/// </summary>
/// <remarks>
/// The context is a strict mock with no setups, so touching <c>Records</c> at all fails the test — the
/// refusal must come before any query. The migrations that do run are covered against SQL Server in
/// <c>ReassignWorkflowIntegrationTests</c>.
/// </remarks>
public sealed class StatusRecordMigratorsTests
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    public StatusRecordMigratorsTests()
    {
        ProductWorkflowOwners.Register();
    }

    private static ProductStatusRecordMigrator CreateSut() =>
        new(new Mock<IProductManagementDbContext>(MockBehavior.Strict).Object);

    private static StatusWorkflow Workflow(string name)
    {
        var workflow = StatusWorkflow.Create(name, null, ProductWorkflowOwners.Product.Key, EventActor.System, Timestamp).Value;
        workflow.AddStatus("Active", null, StatusCategory.Active, (int)ProductStatusAlias.Active, EventActor.System, Timestamp);
        workflow.AddStatus("Retired", null, StatusCategory.Done, (int)ProductStatusAlias.Retired, EventActor.System, Timestamp);
        workflow.Publish(EventActor.System, Timestamp);

        return workflow;
    }

    [Fact]
    public async Task Migrate_ShouldRefuseAScope()
    {
        // Arrange
        var remap = StatusRemap.AutoMap(Workflow("Current"), Workflow("Target")).Value;
        var sut = CreateSut();

        // Act
        var result = await sut.Migrate(
            remap, Guid.CreateVersion7(), EventActor.System, Timestamp, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("organization-wide");
    }

    [Fact]
    public async Task CountByStatus_ShouldRefuseAScope()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.CountByStatus(
            Guid.CreateVersion7(), Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("organization-wide");
    }
}
