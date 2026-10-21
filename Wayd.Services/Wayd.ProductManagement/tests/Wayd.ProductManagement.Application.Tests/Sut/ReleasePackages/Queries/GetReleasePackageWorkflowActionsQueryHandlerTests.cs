using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.DeliveryWorkflows;
using Wayd.ProductManagement.Application.ReleasePackages.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Queries;

/// <summary>
/// The lifecycle actions a release package page may offer, as the package's workflow allows them.
/// </summary>
public sealed class GetReleasePackageWorkflowActionsQueryHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    private GetReleasePackageWorkflowActionsQueryHandler CreateSut() => new(DbContext, _statusResolver.Object);

    private void Governs(StatusWorkflow workflow) =>
        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.ReleasePackage.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workflow));

    [Theory]
    [InlineData(false, new[] { DeliveryAction.MarkReleased, DeliveryAction.Withdraw })]
    [InlineData(true, new[] { DeliveryAction.MarkReleased })]
    public async Task Handle_ShouldOfferWhatAWorkflowWithoutReleasedToWithdrawnAllows(bool released, DeliveryAction[] expected)
    {
        // Arrange
        var workflow = DeliveryWorkflowFactory.DeclaredMovesExcept(ProductWorkflowOwners.ReleasePackage, ("Released", "Withdrawn"));
        Governs(workflow);
        var package = SeedReleasePackage(SeedProduct("checkout").Id, status: DeliveryWorkflowFactory.Initial(workflow));
        if (released)
        {
            package.MarkReleased(Now, DeliveryWorkflowFactory.Ref(workflow, ProductStatusAlias.Released), EventActor.System, Now).IsSuccess.Should().BeTrue();
        }

        // Act
        var result = await CreateSut().Handle(new GetReleasePackageWorkflowActionsQuery(new IdOrKey(package.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        // Withdrawing a released package is optional, and this workflow leaves it out.
        result!.Select(a => a.Action).Should().Equal(expected);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenThePackageDoesNotExist()
    {
        // Act
        var result = await CreateSut().Handle(new GetReleasePackageWorkflowActionsQuery(new IdOrKey(Guid.CreateVersion7().ToString())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
