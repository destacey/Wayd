using FluentAssertions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Commands;

/// <summary>
/// Deleting a release, which leaves the versions and packages it listed in place.
/// </summary>
public sealed class DeleteReleaseCommandHandlerTests : ProductCommandTestBase
{
    private DeleteReleaseCommandHandler CreateSut() =>
        new(DbContext, DbContext, CurrentUser.Object, CurrentPrincipal.Object, Logger<DeleteReleaseCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldDeleteAReleasedReleaseAndItsStatusHistory()
    {
        // Arrange — announced releases are deletable too: it is how a package they list becomes deletable.
        var product = SeedProduct();
        var package = SeedReleasePackage(product.Id);
        var version = SeedVersion(product.Id, "2.0");
        var release = SeedRelease();
        release.SetContents([version.Id], [package.Id], [], EventActor.System, Now);
        release.MarkReleased(Today, false, Status("Released", StatusCategory.Done, ProductStatusAlias.Released), EventActor.System, Now)
            .IsSuccess.Should().BeTrue();
        DbContext.AddStatusTransitions(release.DrainStatusTransitions());
        release.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteReleaseCommand(release.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.Releases.Should().BeEmpty();
        DbContext.StatusTransitions.Should().NotContain(t => t.RecordId == release.Id);
        DbContext.ReleasePackages.Should().ContainSingle(p => p.Id == package.Id);
        DbContext.Versions.Should().Contain(v => v.Id == version.Id);
        release.DomainEvents.OfType<ReleaseDeletedEvent>().Should().ContainSingle()
            .Which.Version.Should().Be(release.Version);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheReleaseDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteReleaseCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
