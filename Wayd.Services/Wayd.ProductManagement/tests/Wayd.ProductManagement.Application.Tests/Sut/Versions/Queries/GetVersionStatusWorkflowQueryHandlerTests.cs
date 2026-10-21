using FluentAssertions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Application.Versions.Queries;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Versions.Queries;

/// <summary>
/// The workflow a version's page draws its status history on.
/// </summary>
public sealed class GetVersionStatusWorkflowQueryHandlerTests : ProductCommandTestBase
{
    private readonly StatusWorkflow _workflow = DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Version);
    private readonly FakeStatusWorkflowDetailsReader _reader;

    public GetVersionStatusWorkflowQueryHandlerTests() => _reader = new(_workflow);

    private GetVersionStatusWorkflowQueryHandler CreateSut() => new(DbContext, _reader, new TransitionConditionChecker([]));

    [Fact]
    public async Task Handle_ShouldReturnTheWorkflowTheVersionIsOn()
    {
        // Arrange
        var product = SeedProduct();
        SeedVersion(product.Id, "0.9");
        var version = SeedVersion(product.Id, status: DeliveryWorkflowFactory.Initial(_workflow));

        // Act
        var result = await CreateSut().Handle(new GetVersionStatusWorkflowQuery(new IdOrKey(version.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(_reader.Details);
        _reader.IsConditionAvailable!(TransitionConditionKind.RequiresReason).Should().BeTrue();
        _reader.IsConditionAvailable!(TransitionConditionKind.RequiresPassedApproval).Should().BeFalse("nothing here evaluates approvals");
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNoSuchVersionExists()
    {
        // Arrange
        SeedVersion(SeedProduct().Id, status: DeliveryWorkflowFactory.Initial(_workflow));

        // Act
        var result = await CreateSut().Handle(new GetVersionStatusWorkflowQuery(new IdOrKey(Guid.CreateVersion7().ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _reader.ReadCount.Should().Be(0);
    }
}
