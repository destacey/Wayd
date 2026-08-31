using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Moving a node in the tree. The handler owns the ancestry walk, because the aggregate's cycle check
/// only works on the chain it is handed.
/// </summary>
public sealed class ReparentProductCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    public ReparentProductCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);
    }

    private ReparentProductCommandHandler CreateSut() =>
        new(_dbContext,
            _currentUser.Object,
            Mock.Of<ILogger<ReparentProductCommandHandler>>(),
            _dateTimeProvider.Object);

    private Product SeedProduct(string name, Guid? parentId = null)
    {
        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active, (int)ProductStatusAlias.Active);

        var product = Product.Create(name, null, Guid.CreateVersion7(), parentId, null, status, EventActor.System, Now);
        _dbContext.AddProduct(product);

        return product;
    }

    [Fact]
    public async Task Handle_ShouldMoveTheNode()
    {
        // Arrange
        var parent = SeedProduct("Suite");
        var child = SeedProduct("Checkout");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new ReparentProductCommand(child.Id, parent.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        child.ParentId.Should().Be(parent.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldMoveANodeToTheRoot()
    {
        // Arrange
        var parent = SeedProduct("Suite");
        var child = SeedProduct("Checkout", parent.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new ReparentProductCommand(child.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        child.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldRefuseAMoveBeneathItsOwnDescendant()
    {
        // Arrange
        var grandparent = SeedProduct("Suite");
        var parent = SeedProduct("Platform", grandparent.Id);
        SeedProduct("Checkout", parent.Id);
        var sut = CreateSut();

        // Act
        // Moving the top of the chain under the bottom of it.
        var result = await sut.Handle(
            new ReparentProductCommand(grandparent.Id, parent.Id), TestContext.Current.CancellationToken);

        // Assert
        // Only caught because the handler walked Platform → Suite and passed the whole chain in; an
        // empty collection here would have silently allowed the cycle.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A product cannot be moved beneath one of its own descendants.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRefuseSelfParenting()
    {
        // Arrange
        var product = SeedProduct("Checkout");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new ReparentProductCommand(product.Id, product.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A product cannot be its own parent.");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheParentDoesNotExist()
    {
        // Arrange
        var product = SeedProduct("Checkout");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ReparentProductCommand(product.Id, Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Parent product not found.");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ReparentProductCommand(Guid.CreateVersion7(), null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }
}
