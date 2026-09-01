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
/// Pulling a release after it was cut. It is never deleted — deployments may reference it.
/// </summary>
public sealed class WithdrawReleaseCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly StatusRef _withdrawn = Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn);

    public WithdrawReleaseCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Release.Key, null, (int)ProductStatusAlias.Withdrawn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(_withdrawn));
    }

    private WithdrawReleaseCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, CurrentPrincipal.Object, Logger<WithdrawReleaseCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldWithdrawTheRelease()
    {
        // Arrange
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new WithdrawReleaseCommand(release.Id, "Critical defect."), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        release.StatusCategory.Should().Be(StatusCategory.Removed);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheRecord()
    {
        // Arrange
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();

        // Act
        await sut.Handle(new WithdrawReleaseCommand(release.Id, null), TestContext.Current.CancellationToken);

        // Assert
        // Withdrawing is a status, not a delete: the release was real and the delivery measures read it.
        DbContext.Releases.Should().ContainSingle();
    }
}
