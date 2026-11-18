using Wayd.Common.Domain.Events.Links;
using Wayd.Links.Commands;
using Wayd.Links.Tests.Infrastructure;

namespace Wayd.Links.Tests.Sut.Commands;

public sealed class UpdateLinkCommandHandlerTests
{
    private readonly LinkTestHarness _harness = new();

    private UpdateLinkCommandHandler CreateHandler() =>
        new(_harness.Resolver(), _harness.Writer(), _harness.DbContext);

    [Fact]
    public async Task Handle_ShouldChangeTheLinkAndSaveOnce()
    {
        // Arrange
        var link = _harness.AddLink();
        var command = new UpdateLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id, "Project charter", "https://example.com/v2");

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        link.Name.Should().Be("Project charter");
        link.Url.Should().Be("https://example.com/v2");
        link.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkDetailsUpdatedEvent>();
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WithoutManagingTheRecord()
    {
        // Arrange
        var link = _harness.AddLink();
        _harness.Owner(canManage: false);
        var command = new UpdateLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id, "Renamed", link.Url);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.CannotManage);
        link.Name.Should().Be("Charter");
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_ForAReadOnlyToken()
    {
        // Arrange
        var link = _harness.AddLink();
        _harness.CurrentPrincipal.SetupGet(p => p.IsReadOnly).Returns(true);
        var command = new UpdateLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id, "Renamed", link.Url);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.CannotManage);
        link.Name.Should().Be("Charter");
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeNotFound_WhenTheRecordCannotBeRead()
    {
        // Arrange
        var link = _harness.AddLink();
        _harness.OwnerHidden();
        var command = new UpdateLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id, "Renamed", link.Url);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.NotFound);
        link.Name.Should().Be("Charter");
    }

    [Fact]
    public void Validator_ShouldLeaveTheAddressToTheLink_SoOneSavedBeforeTheRuleCanStay()
    {
        // Arrange
        var validator = new UpdateLinkCommandValidator();
        var command = new UpdateLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, Guid.NewGuid(), "Archive", "ftp://example.com/file");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "https://example.com")]
    [InlineData("Charter", "")]
    public void Validator_ShouldRequireANameAndAnAddress(string name, string url)
    {
        // Arrange
        var validator = new UpdateLinkCommandValidator();
        var command = new UpdateLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, Guid.NewGuid(), name, url);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldBeNotFound_ForALinkOnAnotherRecord()
    {
        // Arrange
        var link = _harness.AddLink(ownerId: Guid.NewGuid());
        var command = new UpdateLinkCommand(LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id, "Renamed", link.Url);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.NotFound);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
