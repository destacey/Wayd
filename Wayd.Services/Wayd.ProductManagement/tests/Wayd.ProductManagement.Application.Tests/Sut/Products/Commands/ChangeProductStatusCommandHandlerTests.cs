using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Moving a product through its lifecycle. The target is resolved through the governing workflow rather
/// than loaded by id, which is what stops a caller reaching a status from some other workflow.
/// </summary>
public sealed class ChangeProductStatusCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly StatusWorkflow _workflow;
    private readonly WorkflowStatus _active;
    private readonly WorkflowStatus _retired;

    public ChangeProductStatusCommandHandlerTests()
    {
        ProductWorkflowOwners.Register();

        _workflow = StatusWorkflow.CreateSystem("Product Lifecycle", null, ProductWorkflowOwners.Product.Key).Value;
        _active = _workflow.AddSystemStatus("Active", null, StatusCategory.Active, (int)ProductStatusAlias.Active);
        _retired = _workflow.AddSystemStatus("Retired", null, StatusCategory.Done, (int)ProductStatusAlias.Retired);
        _workflow.PublishSystem();

        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.Product.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_workflow));
    }

    private ChangeProductStatusCommandHandler CreateSut() =>
        new(DbContext,
            _statusResolver.Object,
            CurrentUser.Object,
            Logger<ChangeProductStatusCommandHandler>(),
            DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldMoveTheProductToTheTargetStatus()
    {
        // Arrange
        var product = SeedProduct(status: StatusRef.From(_active));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductStatusCommand(product.Id, _retired.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.StatusId.Should().Be(_retired.Id);
        product.StatusName.Should().Be("Retired");
        product.StatusAlias.Should().Be(ProductStatusAlias.Retired);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRecordTheTransition()
    {
        // Arrange
        var product = SeedProduct(status: StatusRef.From(_active));
        var sut = CreateSut();

        // Act
        await sut.Handle(new ChangeProductStatusCommand(product.Id, _retired.Id), TestContext.Current.CancellationToken);

        // Assert
        // The seeded product's opening transition is cleared by the base, so this is the one the command
        // appended — carrying where it came from as well as where it went.
        var transition = product.StatusTransitions.Last();
        transition.FromStatusId.Should().Be(_active.Id);
        transition.ToStatusId.Should().Be(_retired.Id);
    }

    [Fact]
    public async Task Handle_ShouldRaiseTheLifecycleEvent()
    {
        // Arrange
        var product = SeedProduct(status: StatusRef.From(_active));
        var sut = CreateSut();

        // Act
        await sut.Handle(new ChangeProductStatusCommand(product.Id, _retired.Id), TestContext.Current.CancellationToken);

        // Assert
        var raised = product.DomainEvents.OfType<ProductLifecycleChangedEvent>().Should().ContainSingle().Subject;
        raised.ToAlias.Should().Be(ProductStatusAlias.Retired);
    }

    [Fact]
    public async Task Handle_ShouldRaiseNoEvent_WhenAlreadyInThatStatus()
    {
        // Arrange
        var product = SeedProduct(status: StatusRef.From(_active));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductStatusCommand(product.Id, _active.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldRefuseAStatusFromAnotherWorkflow()
    {
        // Arrange
        var other = StatusWorkflow.CreateSystem("Other", null, ProductWorkflowOwners.Product.Key).Value;
        var foreign = other.AddSystemStatus("Sunset", null, StatusCategory.Active, (int)ProductStatusAlias.Sunset);
        var product = SeedProduct(status: StatusRef.From(_active));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductStatusCommand(product.Id, foreign.Id), TestContext.Current.CancellationToken);

        // Assert
        // Resolving through the assignment is the whole point: a status id alone would have moved the
        // product onto a workflow that does not govern it.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("That status does not belong to 'Product Lifecycle'.");
        product.StatusId.Should().Be(_active.Id);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNoWorkflowIsAssigned()
    {
        // Arrange
        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.Product.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<StatusWorkflow>("No workflow is assigned for Product."));

        var product = SeedProduct(status: StatusRef.From(_active));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductStatusCommand(product.Id, _retired.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No workflow is assigned for Product.");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ChangeProductStatusCommand(Guid.CreateVersion7(), _retired.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }
}
