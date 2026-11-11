using FluentAssertions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Incidents;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Commands;

/// <summary>Workflow moves resolve the status on the workflow the incident holds.</summary>
public sealed class ApplyIncidentWorkflowActionCommandHandlerTests : ProductCommandTestBase
{
    private ApplyIncidentWorkflowActionCommandHandler CreateSut() =>
        new(DbContext, DbContext, CurrentUser.Object, CurrentPrincipal.Object, DateTimeProvider.Object, Logger<ApplyIncidentWorkflowActionCommandHandler>());

    private Incident Seed(StatusWorkflow workflow)
    {
        var incident = Incident.Create(
            "Checkout unavailable",
            null,
            null,
            ActingEmployeeId,
            Guid.CreateVersion7(),
            Now,
            null,
            null,
            [],
            DeliveryWorkflowFactory.Initial(workflow),
            EventActor.System,
            Now).Value;
        DbContext.AddIncident(incident);
        return incident;
    }

    [Fact]
    public async Task Handle_ShouldBeginResponding_OnTheWorkflowTheIncidentHolds()
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
        DbContext.AddStatusWorkflow(workflow);
        var incident = Seed(workflow);

        // Act
        var result = await CreateSut().Handle(
            new ApplyIncidentWorkflowActionCommand(incident.Id, IncidentWorkflowAction.BeginResponding, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.StatusAlias.Should().Be(ProductStatusAlias.Responding);
        incident.StartedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldMarkRecovered()
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
        DbContext.AddStatusWorkflow(workflow);
        var incident = Seed(workflow);

        // Act
        var result = await CreateSut().Handle(
            new ApplyIncidentWorkflowActionCommand(incident.Id, IncidentWorkflowAction.MarkRecovered, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.StatusAlias.Should().Be(ProductStatusAlias.Resolved);
        incident.RecoveredAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldClose()
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
        DbContext.AddStatusWorkflow(workflow);
        var incident = Seed(workflow);

        // Act
        var result = await CreateSut().Handle(
            new ApplyIncidentWorkflowActionCommand(incident.Id, IncidentWorkflowAction.Close, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.StatusAlias.Should().Be(ProductStatusAlias.Closed);
    }

    [Fact]
    public async Task Handle_ShouldCancel()
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
        DbContext.AddStatusWorkflow(workflow);
        var incident = Seed(workflow);

        // Act
        var result = await CreateSut().Handle(
            new ApplyIncidentWorkflowActionCommand(incident.Id, IncidentWorkflowAction.Cancel, "Duplicate"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        incident.StatusAlias.Should().Be(ProductStatusAlias.Canceled);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheIncidentWorkflowIsNotTheOneLoaded()
    {
        // Arrange — the incident's status ids belong to a workflow that is not loaded. Another published
        // workflow with the same aliases must not be used in its place.
        var onRecord = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
        var other = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
        DbContext.AddStatusWorkflow(other);
        var incident = Seed(onRecord);

        // Act
        var result = await CreateSut().Handle(
            new ApplyIncidentWorkflowActionCommand(incident.Id, IncidentWorkflowAction.BeginResponding, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        incident.StatusAlias.Should().Be(ProductStatusAlias.Open);
    }
}
