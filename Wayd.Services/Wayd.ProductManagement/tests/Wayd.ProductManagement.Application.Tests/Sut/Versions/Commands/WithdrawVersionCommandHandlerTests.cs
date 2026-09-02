using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Versions.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.Versions.Commands;

/// <summary>
/// Pulling a version after it was cut. It is never deleted — deployments may reference it.
/// </summary>
public sealed class WithdrawVersionCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly StatusRef _withdrawn = Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn);

    public WithdrawVersionCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Version.Key, null, (int)ProductStatusAlias.Withdrawn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(_withdrawn));
    }

    private WithdrawVersionCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, CurrentPrincipal.Object, Logger<WithdrawVersionCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldWithdrawTheRelease()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new WithdrawVersionCommand(version.Id, "Critical defect."), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        version.StatusCategory.Should().Be(StatusCategory.Removed);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheRecord()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var sut = CreateSut();

        // Act
        await sut.Handle(new WithdrawVersionCommand(version.Id, null), TestContext.Current.CancellationToken);

        // Assert
        // Withdrawing is a status, not a delete: the version was real and the delivery measures read it.
        DbContext.Versions.Should().ContainSingle();
    }
}
