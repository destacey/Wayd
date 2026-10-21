using FluentAssertions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Queries;

/// <summary>
/// The workflow a product's page draws its status history on.
/// </summary>
public sealed class GetProductStatusWorkflowQueryHandlerTests : ProductCommandTestBase
{
    private readonly StatusWorkflow _workflow = DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Version);
    private readonly FakeStatusWorkflowDetailsReader _reader;

    public GetProductStatusWorkflowQueryHandlerTests() => _reader = new(_workflow);

    private GetProductStatusWorkflowQueryHandler CreateSut() => new(DbContext, _reader, new TransitionConditionChecker([]));

    [Fact]
    public async Task Handle_ShouldReturnTheWorkflowTheProductIsOn()
    {
        // Arrange
        SeedProduct("Billing");
        var product = SeedProduct(status: new StatusRef(_workflow.Id, Guid.CreateVersion7(), "Planned", StatusCategory.Proposed, StatusWorkflow.NoAlias));

        // Act
        var result = await CreateSut().Handle(new GetProductStatusWorkflowQuery(new IdOrKey(product.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(_reader.Details);
        _reader.IsConditionAvailable!(TransitionConditionKind.RequiresReason).Should().BeTrue();
        _reader.IsConditionAvailable!(TransitionConditionKind.RequiresPassedApproval).Should().BeFalse("nothing here evaluates approvals");
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNoSuchProductExists()
    {
        // Arrange
        SeedProduct(status: new StatusRef(_workflow.Id, Guid.CreateVersion7(), "Planned", StatusCategory.Proposed, StatusWorkflow.NoAlias));

        // Act
        var result = await CreateSut().Handle(new GetProductStatusWorkflowQuery(new IdOrKey(Guid.CreateVersion7().ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _reader.ReadCount.Should().Be(0);
    }
}
