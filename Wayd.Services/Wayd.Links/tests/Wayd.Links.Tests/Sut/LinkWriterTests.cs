using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events.Links;
using Wayd.Common.Domain.Links;
using Wayd.Links.Tests.Infrastructure;

namespace Wayd.Links.Tests.Sut;

public sealed class LinkWriterTests
{
    private readonly LinkTestHarness _harness = new();

    private List<LinkChange> ChangesToldToOwner()
    {
        var changes = new List<LinkChange>();
        _harness.OwnerType.Setup(t => t.OnChanged(Capture.In(changes), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return changes;
    }

    [Fact]
    public async Task Add_ShouldTrackTheLinkAndTellTheOwner_WithoutSaving()
    {
        // Arrange
        var changes = ChangesToldToOwner();

        // Act
        var result = await _harness.Writer().Add(
            LinkTestHarness.Gadget.Key, _harness.OwnerId, "Charter", "https://example.com/charter", _harness.Actor, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var link = _harness.DbContext.Links.Should().ContainSingle().Subject;
        link.Id.Should().Be(result.Value);
        link.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkAddedEvent>();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);

        var change = changes.Should().ContainSingle().Subject;
        change.Kind.Should().Be(LinkChangeKind.Added);
        change.LinkId.Should().Be(link.Id);
        change.OwnerId.Should().Be(_harness.OwnerId);
        change.Details.Should().Be(new LinkDetails("Charter", "https://example.com/charter"));
        change.Previous.Should().BeNull();
        change.Timestamp.Should().Be(_harness.DateTimeProvider.Now);
    }

    [Fact]
    public async Task Add_ShouldAllowASecondLinkToAnAddressTheRecordAlreadyLinksTo()
    {
        // Arrange — an import creates what its file says, and two names for one address are both worth keeping
        _harness.AddLink(url: "https://example.com/charter");

        // Act
        var result = await _harness.Writer().Add(
            LinkTestHarness.Gadget.Key, _harness.OwnerId, "Another name", "https://example.com/charter", _harness.Actor, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _harness.DbContext.Links.Should().HaveCount(2);
    }

    [Fact]
    public async Task Add_ShouldFail_WhenTheAddressIsNotAWebAddress()
    {
        // Arrange — the writer is the last check for a caller with no validator in front of it, such as an import
        var writer = _harness.Writer();

        // Act
        var result = await writer.Add(
            LinkTestHarness.Gadget.Key, _harness.OwnerId, "Charter", "example.com/charter", _harness.Actor, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkErrors.UrlNotWebAddress);
        _harness.DbContext.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_ShouldChangeTheLinkAndTellTheOwnerBothEnds()
    {
        // Arrange
        var link = _harness.AddLink();
        var changes = ChangesToldToOwner();

        // Act
        var result = await _harness.Writer().Update(
            LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id, "Project charter", "https://example.com/v2", _harness.Actor, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        link.Name.Should().Be("Project charter");
        link.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkDetailsUpdatedEvent>();

        var change = changes.Should().ContainSingle().Subject;
        change.Kind.Should().Be(LinkChangeKind.Updated);
        change.Details.Should().Be(new LinkDetails("Project charter", "https://example.com/v2"));
        change.Previous.Should().Be(new LinkDetails("Charter", "https://example.com/charter"));
    }

    [Fact]
    public async Task Update_ShouldTellNoOne_WhenNothingChanged()
    {
        // Arrange
        var link = _harness.AddLink();
        var changes = ChangesToldToOwner();

        // Act
        var result = await _harness.Writer().Update(
            LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id, link.Name, link.Url, _harness.Actor, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        link.DomainEvents.Should().BeEmpty();
        changes.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_ShouldBeNotFound_ForALinkOnAnotherRecord()
    {
        // Arrange
        var link = _harness.AddLink(ownerId: Guid.NewGuid());

        // Act
        var result = await _harness.Writer().Update(
            LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id, "Renamed", link.Url, _harness.Actor, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.NotFound);
        link.Name.Should().Be("Charter");
    }

    [Fact]
    public async Task Remove_ShouldRemoveTheLinkAndTellTheOwner()
    {
        // Arrange
        var link = _harness.AddLink();
        var changes = ChangesToldToOwner();

        // Act
        var result = await _harness.Writer().Remove(
            LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id, _harness.Actor, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _harness.DbContext.Links.Should().BeEmpty();
        link.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkRemovedEvent>();
        changes.Should().ContainSingle().Which.Kind.Should().Be(LinkChangeKind.Removed);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task RemoveAll_ShouldRemoveOnlyThatRecordsLinks_RaisingEachRemoval_WithoutTellingTheOwner()
    {
        // Arrange
        var first = _harness.AddLink(name: "Charter", url: "https://example.com/charter");
        var second = _harness.AddLink(name: "Wiki", url: "https://example.com/wiki");
        var elsewhere = _harness.AddLink(ownerId: Guid.NewGuid());
        var changes = ChangesToldToOwner();

        // Act
        await _harness.Writer().RemoveAll(LinkTestHarness.Gadget.Key, _harness.OwnerId, _harness.Actor, TestContext.Current.CancellationToken);

        // Assert
        _harness.DbContext.Links.Should().ContainSingle().Which.Should().BeSameAs(elsewhere);
        first.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkRemovedEvent>();
        second.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkRemovedEvent>();
        elsewhere.DomainEvents.Should().BeEmpty();
        changes.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAll_ShouldThrow_ForAnUnregisteredOwnerType()
    {
        // Arrange
        var writer = _harness.Writer();

        // Act
        var act = () => writer.RemoveAll("test.link-writer.unregistered", _harness.OwnerId, _harness.Actor, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
