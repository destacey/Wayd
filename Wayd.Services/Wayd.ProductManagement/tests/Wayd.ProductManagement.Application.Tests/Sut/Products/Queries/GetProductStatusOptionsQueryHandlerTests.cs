using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Queries;

/// <summary>
/// The statuses a product picker offers: every status, or only the moves a product's workflow allows.
/// </summary>
public sealed class GetProductStatusOptionsQueryHandlerTests : ProductCommandTestBase
{
    private static readonly Instant At = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly StatusWorkflow _workflow;

    public GetProductStatusOptionsQueryHandlerTests()
    {
        ProductWorkflowOwners.Register();

        _workflow = StatusWorkflow.Create("Gated Lifecycle", null, ProductWorkflowOwners.Product.Key, EventActor.System, At).Value;
        _workflow.AddStatus("Concept", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, At);
        _workflow.AddStatus("Active", null, StatusCategory.Active, (int)ProductStatusAlias.Active, EventActor.System, At);
        _workflow.AddStatus("Sunset", null, StatusCategory.Active, (int)ProductStatusAlias.Sunset, EventActor.System, At);
        _workflow.AddStatus("Retired", null, StatusCategory.Done, (int)ProductStatusAlias.Retired, EventActor.System, At);

        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.Product.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(_workflow));
    }

    private GetProductStatusOptionsQueryHandler CreateSut() => new(DbContext, _statusResolver.Object);

    private WorkflowStatus Status(string name) => _workflow.Statuses.Single(s => s.Name == name);

    private void Restrict()
    {
        _workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At);
        _workflow.SetTransitions(
            [(Status("Concept").Id, Status("Active").Id), (Status("Active").Id, Status("Sunset").Id), (Status("Sunset").Id, Status("Retired").Id)],
            EventActor.System, At);
    }

    [Fact]
    public async Task Handle_ShouldOfferEveryStatusInOrder_WhenNoProductIsNamed()
    {
        // Arrange
        Restrict();

        // Act
        var result = await CreateSut().Handle(new GetProductStatusOptionsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result!.Select(s => s.Name).Should().Equal("Concept", "Active", "Sunset", "Retired");
    }

    [Fact]
    public async Task Handle_ShouldOfferOnlyTheAllowedMoves_ForAProductUnderARestrictedWorkflow()
    {
        // Arrange
        Restrict();
        var product = SeedProduct(status: StatusRef.From(Status("Concept")));

        // Act
        var result = await CreateSut().Handle(new GetProductStatusOptionsQuery(product.Id), TestContext.Current.CancellationToken);

        // Assert
        var option = result.Should().ContainSingle().Subject;
        option.Id.Should().Be(Status("Active").Id);
        option.Alias.Should().Be((int)ProductStatusAlias.Active);
    }

    [Fact]
    public async Task Handle_ShouldOfferEveryOtherStatus_ForAProductUnderAnAnyToAnyWorkflow()
    {
        // Arrange
        var product = SeedProduct(status: StatusRef.From(Status("Active")));

        // Act
        var result = await CreateSut().Handle(new GetProductStatusOptionsQuery(product.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.Select(s => s.Name).Should().Equal("Concept", "Sunset", "Retired");
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheProductDoesNotExist()
    {
        // Arrange
        SeedProduct(status: StatusRef.From(Status("Concept")));

        // Act
        var result = await CreateSut().Handle(new GetProductStatusOptionsQuery(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
