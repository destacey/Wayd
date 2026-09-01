using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Commands;

/// <summary>
/// Cutting a release. Resolves the Ready status by alias, so a renamed workflow status still works.
/// </summary>
public sealed class CutReleaseCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly StatusRef _ready = Status("Ready", StatusCategory.Active, ProductStatusAlias.Ready);

    public CutReleaseCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Release.Key, null, (int)ProductStatusAlias.Ready, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(_ready));
    }

    private CutReleaseCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, CurrentPrincipal.Object, Logger<CutReleaseCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldCutTheRelease()
    {
        // Arrange
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CutReleaseCommand(release.Id, new LocalDate(2026, 5, 1)), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        release.CutDate.Should().Be(new LocalDate(2026, 5, 1));
        release.StatusId.Should().Be(_ready.StatusId);
    }

    [Fact]
    public async Task Handle_ShouldResolveTheStatusByAliasRatherThanById()
    {
        // Arrange
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new CutReleaseCommand(release.Id, new LocalDate(2026, 5, 1)), TestContext.Current.CancellationToken);

        // Assert
        // Asking for the meaning rather than a fixed id is what lets an organization rename or reorder
        // its workflow without breaking the transition.
        _statusResolver.Verify(
            r => r.ForAlias(
                ProductWorkflowOwners.Release.Key, null, (int)ProductStatusAlias.Ready, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRefuseARereCut()
    {
        // Arrange
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();
        await sut.Handle(new CutReleaseCommand(release.Id, new LocalDate(2026, 5, 1)), TestContext.Current.CancellationToken);

        // Act
        var result = await sut.Handle(
            new CutReleaseCommand(release.Id, new LocalDate(2026, 6, 1)), TestContext.Current.CancellationToken);

        // Assert
        // Cutting freezes scope, so doing it twice would silently move the line.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This release has already been cut.");
        release.CutDate.Should().Be(new LocalDate(2026, 5, 1));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheAliasIsMissingFromTheWorkflow()
    {
        // Arrange
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Release.Key, null, (int)ProductStatusAlias.Ready, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<StatusRef>("'Custom' has no status for Ready."));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CutReleaseCommand(release.Id, new LocalDate(2026, 5, 1)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Custom' has no status for Ready.");
        release.CutDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheReleaseDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CutReleaseCommand(Guid.CreateVersion7(), new LocalDate(2026, 5, 1)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release not found.");
    }
}
