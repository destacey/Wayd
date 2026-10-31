using FluentAssertions;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

public sealed class UnlinkProductTeamCommandHandlerTests : ProductCommandTestBase
{
    private UnlinkProductTeamCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<UnlinkProductTeamCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldUnlinkTheTeam()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var team = SeedTeam();
        SeedTeamLink(web, team);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new UnlinkProductTeamCommand(web.Id, team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        web.TeamLinks.Should().BeEmpty();
        web.DomainEvents.OfType<ProductTeamUnlinkedEvent>().Should().ContainSingle()
            .Which.TeamId.Should().Be(team.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheTeamIsNotLinked_ShouldSucceedWithoutChange()
    {
        // Arrange
        var web = SeedProduct("Storefront Web");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new UnlinkProductTeamCommand(web.Id, Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        web.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheProductDoesNotExist_ShouldFail()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new UnlinkProductTeamCommand(Guid.CreateVersion7(), Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
