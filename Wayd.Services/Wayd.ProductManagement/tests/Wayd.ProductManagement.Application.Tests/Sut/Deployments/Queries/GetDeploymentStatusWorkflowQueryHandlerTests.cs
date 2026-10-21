using FluentAssertions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Deployments.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Deployments.Queries;

/// <summary>
/// The workflow a deployment's page draws its status history on.
/// </summary>
public sealed class GetDeploymentStatusWorkflowQueryHandlerTests : ProductCommandTestBase
{
    private readonly StatusWorkflow _workflow = DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Deployment);
    private readonly FakeStatusWorkflowDetailsReader _reader;

    public GetDeploymentStatusWorkflowQueryHandlerTests() => _reader = new(_workflow);

    private GetDeploymentStatusWorkflowQueryHandler CreateSut() => new(DbContext, _reader, new TransitionConditionChecker([]));

    /// <summary>A deployment on the workflow under test, which <c>SeedDeployment</c> cannot place it on.</summary>
    private Deployment SeedOnWorkflow()
    {
        var environment = SeedEnvironment();
        var deployment = Deployment.Create(
            SeedVersion(SeedProduct().Id).Id,
            null,
            environment.Id,
            environment.Category,
            null,
            Now,
            DeliveryWorkflowFactory.Ref(_workflow, ProductStatusAlias.InProgress),
            environment.Name,
            EventActor.System,
            Now).Value;

        DbContext.AddDeployment(deployment);

        return deployment;
    }

    [Fact]
    public async Task Handle_ShouldReturnTheWorkflowTheDeploymentIsOn()
    {
        // Arrange
        SeedDeployment();
        var deployment = SeedOnWorkflow();

        // Act
        var result = await CreateSut().Handle(new GetDeploymentStatusWorkflowQuery(new IdOrKey(deployment.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(_reader.Details);
        _reader.IsConditionAvailable!(TransitionConditionKind.RequiresReason).Should().BeTrue();
        _reader.IsConditionAvailable!(TransitionConditionKind.RequiresPassedApproval).Should().BeFalse("nothing here evaluates approvals");
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNoSuchDeploymentExists()
    {
        // Arrange
        SeedOnWorkflow();

        // Act
        var result = await CreateSut().Handle(new GetDeploymentStatusWorkflowQuery(new IdOrKey(Guid.CreateVersion7().ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _reader.ReadCount.Should().Be(0);
    }
}
