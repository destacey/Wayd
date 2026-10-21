using FluentAssertions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Queries;

/// <summary>
/// The workflow a package's page draws its status history on.
/// </summary>
public sealed class GetReleasePackageStatusWorkflowQueryHandlerTests : ProductCommandTestBase
{
    private readonly StatusWorkflow _workflow = DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.ReleasePackage);
    private readonly FakeStatusWorkflowDetailsReader _reader;

    public GetReleasePackageStatusWorkflowQueryHandlerTests() => _reader = new(_workflow);

    private GetReleasePackageStatusWorkflowQueryHandler CreateSut() => new(DbContext, _reader, new TransitionConditionChecker([]));

    [Fact]
    public async Task Handle_ShouldReturnTheWorkflowThePackageIsOn()
    {
        // Arrange
        var product = SeedProduct();
        SeedReleasePackage(product.Id, "2026.13");
        var package = SeedReleasePackage(product.Id, status: DeliveryWorkflowFactory.Initial(_workflow));

        // Act
        var result = await CreateSut().Handle(new GetReleasePackageStatusWorkflowQuery(new IdOrKey(package.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(_reader.Details);
        _reader.IsConditionAvailable!(TransitionConditionKind.RequiresReason).Should().BeTrue();
        _reader.IsConditionAvailable!(TransitionConditionKind.RequiresPassedApproval).Should().BeFalse("nothing here evaluates approvals");
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNoSuchPackageExists()
    {
        // Arrange
        SeedReleasePackage(SeedProduct().Id, status: DeliveryWorkflowFactory.Initial(_workflow));

        // Act
        var result = await CreateSut().Handle(new GetReleasePackageStatusWorkflowQuery(new IdOrKey(Guid.CreateVersion7().ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _reader.ReadCount.Should().Be(0);
    }
}
