using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Commands;

/// <summary>
/// Planning a release — drafting an announcement before it carries anything.
/// </summary>
public sealed class PlanReleaseCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    public PlanReleaseCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.Release.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(
                Status("Planned", StatusCategory.Proposed, ProductStatusAlias.None)));
    }

    private PlanReleaseCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, CurrentPrincipal.Object, Logger<PlanReleaseCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldPlanTheRelease()
    {
        // Arrange
        var product = SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new PlanReleaseCommand(product.Id, "2026.07", "July", null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(1);
        var release = DbContext.Releases.Should().ContainSingle().Subject;
        release.ProductId.Should().Be(product.Id);
        release.Version.Should().Be("2026.07");
    }

    [Fact]
    public async Task Handle_ShouldPlanAReleaseSpanningProductLines()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new PlanReleaseCommand(null, "2026.07", null, null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.Releases.Should().ContainSingle().Which.ProductId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldAttributeTheOpeningTransitionToTheActingEmployee()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new PlanReleaseCommand(null, "2026.07", null, null, null), TestContext.Current.CancellationToken);

        // Assert
        var release = DbContext.Releases.Should().ContainSingle().Subject;
        release.StatusTransitions.Should().ContainSingle().Which.ActorEmployeeId.Should().Be(ActingEmployeeId);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new PlanReleaseCommand(Guid.CreateVersion7(), "2026.07", null, null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheWorkflowCannotBeResolved()
    {
        // Arrange
        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.Release.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<StatusRef>("No workflow is assigned."));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new PlanReleaseCommand(null, "2026.07", null, null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No workflow is assigned.");
        DbContext.Releases.Should().BeEmpty();
    }
}
