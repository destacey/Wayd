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

    private void Restrict(params TransitionConditionKind[] retireConditions)
    {
        _workflow.SetTransitionMode(TransitionMode.Restricted, EventActor.System, At);
        _workflow.SetTransitions(
            [
                new(Status("Concept").Id, Status("Active").Id),
                new(Status("Active").Id, Status("Sunset").Id),
                new(Status("Active").Id, Status("Retired").Id),
                new(Status("Sunset").Id, Status("Retired").Id, retireConditions),
            ],
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
    public async Task Handle_ShouldSayWhatEachAllowedMoveNeeds()
    {
        // Arrange
        Restrict(TransitionConditionKind.RequiresReason);
        var fromSunset = SeedProduct(status: StatusRef.From(Status("Sunset")));
        var fromActive = SeedProduct(status: StatusRef.From(Status("Active")));

        // Act
        var sunsetMoves = await CreateSut().Handle(new GetProductStatusOptionsQuery(fromSunset.Id), TestContext.Current.CancellationToken);
        var activeMoves = await CreateSut().Handle(new GetProductStatusOptionsQuery(fromActive.Id), TestContext.Current.CancellationToken);

        // Assert
        // Retired needs a reason from Sunset, but not from Active: the condition belongs to the move.
        sunsetMoves.Should().ContainSingle().Which.Conditions.Should().Equal(TransitionConditionKind.RequiresReason);
        activeMoves!.Single(s => s.Id == Status("Retired").Id).Conditions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldListNoConditions_WhenNoProductIsNamed()
    {
        // Arrange
        Restrict(TransitionConditionKind.RequiresReason);

        // Act
        var result = await CreateSut().Handle(new GetProductStatusOptionsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result!.Should().OnlyContain(s => s.Conditions.Count == 0);
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
