using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Incidents;
using Wayd.ProductManagement.Application.Incidents.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Queries;

/// <summary>
/// The lifecycle actions an incident page may offer, as the incident's workflow allows them.
/// </summary>
public sealed class GetIncidentWorkflowActionsQueryHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    private GetIncidentWorkflowActionsQueryHandler CreateSut() =>
        new(DbContext, DbContext, _statusResolver.Object);

    private void Governs(StatusWorkflow workflow) =>
        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.Incident.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workflow));

    private Incident SeedResponding(StatusWorkflow workflow)
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

        incident.BeginResponding(
                DeliveryWorkflowFactory.Ref(workflow, ProductStatusAlias.Responding),
                EventActor.System,
                Now)
            .IsSuccess.Should().BeTrue();

        DbContext.AddIncident(incident);

        return incident;
    }

    [Fact]
    public async Task Handle_ShouldOfferMarkRecoveredAndCancel_FromRespondingUnderTheDefaultWorkflow()
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.RequiredMovesOnly(ProductWorkflowOwners.Incident);
        Governs(workflow);
        var incident = SeedResponding(workflow);

        // Act
        var result = await CreateSut().Handle(
            new GetIncidentWorkflowActionsQuery(new IdOrKey(incident.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result!.Select(a => a.Action).Should().Equal(
            IncidentWorkflowAction.MarkRecovered,
            IncidentWorkflowAction.Cancel);
    }

    [Fact]
    public async Task Handle_ShouldUseTheWorkflowTheIncidentHolds_NotTheOrgAssignment()
    {
        // Arrange — status ids live on one workflow; the assignment resolves another with the same
        // aliases but different ids, which makes AllowedTargets empty when the wrong graph is queried.
        var onRecord = DeliveryWorkflowFactory.RequiredMovesOnly(ProductWorkflowOwners.Incident);
        DbContext.AddStatusWorkflow(onRecord);
        var assigned = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
        DbContext.AddStatusWorkflow(assigned);
        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.Incident.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(assigned));

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
            DeliveryWorkflowFactory.Initial(onRecord),
            EventActor.System,
            Now).Value;
        incident.BeginResponding(
                DeliveryWorkflowFactory.Ref(onRecord, ProductStatusAlias.Responding),
                EventActor.System,
                Now)
            .IsSuccess.Should().BeTrue();
        DbContext.AddIncident(incident);

        // Act
        var result = await CreateSut().Handle(
            new GetIncidentWorkflowActionsQuery(new IdOrKey(incident.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result!.Select(a => a.Action).Should().Equal(
            IncidentWorkflowAction.MarkRecovered,
            IncidentWorkflowAction.Cancel);
    }

    [Fact]
    public async Task Handle_ShouldOfferEveryAction_UnderAnAnyToAnyWorkflow()
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Incident));
        Governs(workflow);
        var incident = SeedResponding(workflow);

        // Act
        var result = await CreateSut().Handle(
            new GetIncidentWorkflowActionsQuery(new IdOrKey(incident.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert — from Responding, only forward moves (not Begin Responding to the status already held)
        result!.Select(a => a.Action).Should().Equal(
            IncidentWorkflowAction.MarkRecovered,
            IncidentWorkflowAction.Close,
            IncidentWorkflowAction.Cancel);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheIncidentDoesNotExist()
    {
        // Act
        var result = await CreateSut().Handle(
            new GetIncidentWorkflowActionsQuery(new IdOrKey(Guid.CreateVersion7().ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
