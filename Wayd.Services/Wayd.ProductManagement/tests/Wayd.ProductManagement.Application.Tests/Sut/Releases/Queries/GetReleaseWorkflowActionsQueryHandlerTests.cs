using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.DeliveryWorkflows;
using Wayd.ProductManagement.Application.Releases.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Queries;

/// <summary>
/// The lifecycle actions a release page may offer, as the release's workflow allows them.
/// </summary>
public sealed class GetReleaseWorkflowActionsQueryHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    private GetReleaseWorkflowActionsQueryHandler CreateSut() => new(DbContext, _statusResolver.Object);

    private void Governs(StatusWorkflow workflow) =>
        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.Release.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workflow));

    [Theory]
    [InlineData("Ready", DeliveryAction.Withdraw)]
    [InlineData("Withdrawn", DeliveryAction.RevertRelease)]
    public async Task Handle_ShouldLeaveOutTheOptionalMove_AReleasedReleasesWorkflowLeavesOut(string leftOut, DeliveryAction stillOffered)
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.DeclaredMovesExcept(ProductWorkflowOwners.Release, ("Released", leftOut));
        Governs(workflow);
        var release = SeedRelease(status: DeliveryWorkflowFactory.Initial(workflow));
        release.MarkReleased(Today, false, DeliveryWorkflowFactory.Ref(workflow, ProductStatusAlias.Released), EventActor.System, Now).IsSuccess.Should().BeTrue();

        // Act
        var result = await CreateSut().Handle(new GetReleaseWorkflowActionsQuery(new IdOrKey(release.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        var omitted = leftOut == "Ready" ? DeliveryAction.RevertRelease : DeliveryAction.Withdraw;
        result!.Select(a => a.Action).Should().Contain([DeliveryAction.MarkReleased, stillOffered]).And.NotContain(omitted);
    }

    [Fact]
    public async Task Handle_ShouldOfferEveryAction_UnderAnAnyToAnyWorkflow()
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Release));
        Governs(workflow);
        var release = SeedRelease(status: DeliveryWorkflowFactory.Initial(workflow));

        // Act
        var result = await CreateSut().Handle(new GetReleaseWorkflowActionsQuery(new IdOrKey(release.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        result!.Select(a => a.Action).Should().Equal(DeliveryAction.MarkReleased, DeliveryAction.RevertRelease, DeliveryAction.Withdraw);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheReleaseDoesNotExist()
    {
        // Act
        var result = await CreateSut().Handle(new GetReleaseWorkflowActionsQuery(new IdOrKey(Guid.CreateVersion7().ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
