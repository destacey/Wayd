using Wayd.Common.Domain.Events.Links;
using Wayd.Links.Commands;
using Wayd.Links.Tests.Infrastructure;

namespace Wayd.Links.Tests.Sut.Commands;

public sealed class DeleteLinkCommandHandlerTests
{
    private readonly LinkTestHarness _harness = new();

    private DeleteLinkCommandHandler CreateHandler() =>
        new(_harness.Resolver(), _harness.Writer(), _harness.DbContext);

    [Fact]
    public async Task Handle_ShouldRemoveTheLinkAndSaveOnce()
    {
        // Arrange
        var link = _harness.AddLink();
        var command = new DeleteLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _harness.DbContext.Links.Should().BeEmpty();
        link.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkRemovedEvent>();
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WithoutManagingTheRecord()
    {
        // Arrange
        var link = _harness.AddLink();
        _harness.Owner(canManage: false);
        var command = new DeleteLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.CannotManage);
        _harness.DbContext.Links.Should().ContainSingle();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeNotFound_ForALinkOnAnotherRecord()
    {
        // Arrange — managing one record gives nothing on the links of another
        var elsewhere = _harness.AddLink(ownerId: Guid.NewGuid());
        var command = new DeleteLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, elsewhere.Id);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.NotFound);
        _harness.DbContext.Links.Should().ContainSingle();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeNotFound_ForALinkTheRecordDoesNotHave()
    {
        // Arrange
        var command = new DeleteLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.NotFound);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
