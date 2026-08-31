using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Creating a product node. The handler's own work is resolving the opening status and refusing the
/// references it cannot honour — the aggregate covers the rest.
/// </summary>
public sealed class CreateProductCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    public CreateProductCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.Product.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(ActiveStatus()));
    }

    private static StatusRef ActiveStatus() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active, (int)ProductStatusAlias.Active);

    private CreateProductCommandHandler CreateSut() =>
        new(_dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            Mock.Of<ILogger<CreateProductCommandHandler>>(),
            _dateTimeProvider.Object);

    private ProductType SeedType(bool isReleasable = true, bool isActive = true)
    {
        var productType = ProductType.Create("Application", null, isReleasable, 1);

        if (!isActive)
        {
            productType.Deactivate();
        }

        _dbContext.AddProductType(productType);

        return productType;
    }

    [Fact]
    public async Task Handle_ShouldCreateTheProduct()
    {
        // Arrange
        var productType = SeedType();
        var sut = CreateSut();
        var command = new CreateProductCommand("Checkout", null, productType.Id, null, null);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _dbContext.Products.Should().ContainSingle().Which.Name.Should().Be("Checkout");
    }

    [Fact]
    public async Task Handle_ShouldOpenInTheWorkflowsInitialStatus()
    {
        // Arrange
        var productType = SeedType();
        var sut = CreateSut();
        var command = new CreateProductCommand("Checkout", null, productType.Id, null, null);

        // Act
        await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        // The status comes from the assigned workflow, never from a constant in the handler — that is
        // the whole reason the resolver exists.
        var product = _dbContext.Products.Single();
        product.StatusName.Should().Be("Active");
        product.StatusAlias.Should().Be(ProductStatusAlias.Active);
    }

    [Fact]
    public async Task Handle_ShouldRecordTheOpeningTransition()
    {
        // Arrange
        var productType = SeedType();
        var sut = CreateSut();
        var command = new CreateProductCommand("Checkout", null, productType.Id, null, null);

        // Act
        await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _dbContext.Products.Single().StatusTransitions.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheWorkflowCannotBeResolved()
    {
        // Arrange
        var productType = SeedType();
        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.Product.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<StatusRef>("No workflow is assigned for Product."));

        var sut = CreateSut();
        var command = new CreateProductCommand("Checkout", null, productType.Id, null, null);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        // Misconfiguration surfaces as the resolver's own message; nothing is written.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No workflow is assigned for Product.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductTypeDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();
        var command = new CreateProductCommand("Checkout", null, Guid.CreateVersion7(), null, null);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product Type not found.");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductTypeIsInactive()
    {
        // Arrange
        var productType = SeedType(isActive: false);
        var sut = CreateSut();
        var command = new CreateProductCommand("Checkout", null, productType.Id, null, null);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        // Deactivating retires a type from new use without disturbing what already carries it.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheParentDoesNotExist()
    {
        // Arrange
        var productType = SeedType();
        var sut = CreateSut();
        var command = new CreateProductCommand("Checkout", null, productType.Id, Guid.CreateVersion7(), null);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Parent product not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
