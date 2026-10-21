using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.DeliveryWorkflows;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Application.Versions.Queries;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Tests.Data;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.Versions.Queries;

/// <summary>
/// The lifecycle actions a version page may offer, as the version's workflow allows them.
/// </summary>
public sealed class GetVersionWorkflowActionsQueryHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    private GetVersionWorkflowActionsQueryHandler CreateSut() => new(DbContext, _statusResolver.Object);

    private void Governs(StatusWorkflow workflow) =>
        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.Version.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workflow));

    private Version SeedUnder(StatusWorkflow workflow, bool cut, bool released)
    {
        var version = SeedVersion(SeedProduct("checkout").Id, status: DeliveryWorkflowFactory.Initial(workflow));

        if (cut)
        {
            version.Cut(Now, DeliveryWorkflowFactory.Ref(workflow, ProductStatusAlias.Ready), "Checkout", EventActor.System, Now).IsSuccess.Should().BeTrue();
        }

        if (released)
        {
            version.MarkReleased(Now, DeliveryWorkflowFactory.Ref(workflow, ProductStatusAlias.Released), "Checkout", EventActor.System, Now).IsSuccess.Should().BeTrue();
        }

        return version;
    }

    private async Task<IReadOnlyCollection<DeliveryWorkflowActionDto>?> Run(Version version) =>
        await CreateSut().Handle(new GetVersionWorkflowActionsQuery(new IdOrKey(version.Id.ToString())), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_ShouldOfferEveryAction_UnderAnAnyToAnyWorkflow()
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.Published(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Version));
        Governs(workflow);
        var version = SeedUnder(workflow, cut: false, released: false);

        // Act
        var result = await Run(version);

        // Assert
        result!.Select(a => a.Action).Should().Equal(
            DeliveryAction.Cut, DeliveryAction.MarkReleased, DeliveryAction.RevertRelease, DeliveryAction.Withdraw);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutWithdraw_ForAReleasedVersion_WhenTheWorkflowLeavesOutReleasedToWithdrawn()
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.DeclaredMovesExcept(ProductWorkflowOwners.Version, ("Released", "Withdrawn"));
        Governs(workflow);
        var version = SeedUnder(workflow, cut: true, released: true);

        // Act
        var result = await Run(version);

        // Assert
        // Answers for the workflow alone: Released → Ready is allowed, so Cut is too, though the version
        // refuses a second cut. The page applies the version's own rules on top.
        result!.Select(a => a.Action).Should().Equal(DeliveryAction.Cut, DeliveryAction.MarkReleased, DeliveryAction.RevertRelease);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Handle_ShouldOfferRevert_ByWhereTheVersionWouldReturnTo(bool cut, bool offered)
    {
        // Arrange
        // Only the revert of a never-cut version — back to the initial status — is left out.
        var workflow = DeliveryWorkflowFactory.DeclaredMovesExcept(ProductWorkflowOwners.Version, ("Released", "Planned"));
        Governs(workflow);
        var version = SeedUnder(workflow, cut, released: true);

        // Act
        var result = await Run(version);

        // Assert
        result!.Any(a => a.Action == DeliveryAction.RevertRelease).Should().Be(offered);
    }

    [Fact]
    public async Task Handle_ShouldOfferNothing_WhenNoWorkflowCanBeResolved()
    {
        // Arrange
        var version = SeedUnder(DeliveryWorkflowFactory.Seeded(ProductWorkflowOwners.Version), cut: false, released: false);
        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.Version.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<StatusWorkflow>("No workflow is assigned for Version."));

        // Act
        var result = await Run(version);

        // Assert
        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheVersionDoesNotExist()
    {
        // Act
        var result = await CreateSut().Handle(new GetVersionWorkflowActionsQuery(new IdOrKey(Guid.CreateVersion7().ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _statusResolver.VerifyNoOtherCalls();
    }
}
