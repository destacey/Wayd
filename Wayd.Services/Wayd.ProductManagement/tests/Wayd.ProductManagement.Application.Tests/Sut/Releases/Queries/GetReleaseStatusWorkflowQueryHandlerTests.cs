using FluentAssertions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Queries;

/// <summary>
/// The workflow a release's page draws its status history on.
/// </summary>
public sealed class GetReleaseStatusWorkflowQueryHandlerTests : ProductCommandTestBase
{
    private readonly StatusWorkflow _workflow = DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Release);
    private readonly FakeStatusWorkflowDetailsReader _reader;

    public GetReleaseStatusWorkflowQueryHandlerTests() => _reader = new(_workflow);

    private GetReleaseStatusWorkflowQueryHandler CreateSut() => new(DbContext, _reader, new TransitionConditionChecker([]));

    [Fact]
    public async Task Handle_ShouldReturnTheWorkflowTheReleaseIsOn()
    {
        // Arrange
        SeedRelease(version: "2026.06");
        var release = SeedRelease(status: DeliveryWorkflowFactory.Initial(_workflow));

        // Act
        var result = await CreateSut().Handle(new GetReleaseStatusWorkflowQuery(new IdOrKey(release.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(_reader.Details);
        _reader.IsConditionAvailable!(TransitionConditionKind.RequiresReason).Should().BeTrue();
        _reader.IsConditionAvailable!(TransitionConditionKind.RequiresPassedApproval).Should().BeFalse("nothing here evaluates approvals");
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNoSuchReleaseExists()
    {
        // Arrange
        SeedRelease(status: DeliveryWorkflowFactory.Initial(_workflow));

        // Act
        var result = await CreateSut().Handle(new GetReleaseStatusWorkflowQuery(new IdOrKey(Guid.CreateVersion7().ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _reader.ReadCount.Should().Be(0);
    }
}
