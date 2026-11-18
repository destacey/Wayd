using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Links;
using Wayd.Common.Domain.Links;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Common.Domain.Tests.Sut.Links;

public sealed class LinkTests
{
    private static readonly LinkOwnerTypeDescriptor Widget = new("test.link-model.widget", "Widget", "Widget");
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 17, 9, 30);
    private static readonly EventActor Actor = EventActor.User("user-1", Guid.NewGuid());

    private readonly Guid _ownerId = Guid.NewGuid();

    public LinkTests()
    {
        LinkOwnerTypes.Register(Widget);
    }

    private Link CreateLink(string name = "Charter", string url = "https://example.com/charter")
    {
        var link = Link.Create(Widget.Key, _ownerId, name, url, Actor, Now).Value;
        link.ClearDomainEvents();
        return link;
    }

    [Fact]
    public void Create_ShouldStoreTheRegisteredKeyAndTrimmedValues_AndRaiseLinkAdded()
    {
        // Arrange — the owner type is given in another casing

        // Act
        var result = Link.Create(" TEST.Link-Model.Widget ", _ownerId, "  Charter ", " https://example.com/charter ", Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var link = result.Value;
        link.OwnerType.Should().Be(Widget.Key);
        link.OwnerId.Should().Be(_ownerId);
        link.Name.Should().Be("Charter");
        link.Url.Should().Be("https://example.com/charter");

        var added = link.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkAddedEvent>().Subject;
        added.Id.Should().Be(link.Id);
        added.OwnerType.Should().Be(Widget.Key);
        added.OwnerId.Should().Be(_ownerId);
        added.Name.Should().Be("Charter");
        added.Url.Should().Be("https://example.com/charter");
        added.RelatedAggregates.Should().ContainSingle().Which.Should().Be(new AggregateReference("Widget", _ownerId));
    }

    [Fact]
    public void Create_ShouldFail_ForAnUnregisteredOwnerType()
    {
        // Arrange
        const string key = "test.link-model.unregistered";

        // Act
        var result = Link.Create(key, _ownerId, "Charter", "https://example.com", Actor, Now);

        // Assert
        result.Error.Should().Be(LinkErrors.UnknownOwnerType(key));
    }

    [Theory]
    [InlineData("example.com/charter")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/file")]
    [InlineData("//example.com")]
    [InlineData("/relative/path")]
    public void Create_ShouldFail_WhenTheUrlIsNotAWebAddress(string url)
    {
        // Arrange — the URL is the theory data

        // Act
        var result = Link.Create(Widget.Key, _ownerId, "Charter", url, Actor, Now);

        // Assert
        result.Error.Should().Be(LinkErrors.UrlNotWebAddress);
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseLinkDetailsUpdated_WithBothEnds()
    {
        // Arrange
        var link = CreateLink();

        // Act
        var result = link.UpdateDetails("Project charter", "http://example.com/v2", Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        link.Name.Should().Be("Project charter");
        link.Url.Should().Be("http://example.com/v2");

        var updated = link.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkDetailsUpdatedEvent>().Subject;
        updated.Details.Should().Be(new LinkDetails("Project charter", "http://example.com/v2"));
        updated.Previous.Should().Be(new LinkDetails("Charter", "https://example.com/charter"));
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseNothing_WhenOnlyPaddingDiffers()
    {
        // Arrange
        var link = CreateLink();

        // Act
        var result = link.UpdateDetails(" Charter ", " https://example.com/charter ", Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        link.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_ShouldFailAndChangeNothing_WhenTheNewUrlIsNotAWebAddress()
    {
        // Arrange
        var link = CreateLink();

        // Act
        var result = link.UpdateDetails("Renamed", "example.com", Actor, Now);

        // Assert
        result.Error.Should().Be(LinkErrors.UrlNotWebAddress);
        link.Name.Should().Be("Charter");
        link.Url.Should().Be("https://example.com/charter");
        link.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_ShouldRenameALinkWhoseAddressPredatesTheWebAddressRule()
    {
        // Arrange — an address saved before addresses were checked
        var link = CreateLink();
        link.SetPrivate(l => l.Url, "ftp://example.com/file");

        // Act
        var result = link.UpdateDetails("Archive", "ftp://example.com/file", Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        link.Name.Should().Be("Archive");
        link.Url.Should().Be("ftp://example.com/file");
        link.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkDetailsUpdatedEvent>();
    }

    [Fact]
    public void UpdateDetails_ShouldRefuseAnotherAddressThatIsNotAWebAddress_EvenFromOne()
    {
        // Arrange
        var link = CreateLink();
        link.SetPrivate(l => l.Url, "ftp://example.com/file");

        // Act
        var result = link.UpdateDetails("Archive", "ftp://example.com/other", Actor, Now);

        // Assert
        result.Error.Should().Be(LinkErrors.UrlNotWebAddress);
        link.Url.Should().Be("ftp://example.com/file");
    }

    [Fact]
    public void Remove_ShouldRaiseLinkRemoved_WithWhatTheLinkSaid()
    {
        // Arrange
        var link = CreateLink();

        // Act
        link.Remove(Actor, Now);

        // Assert
        var removed = link.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LinkRemovedEvent>().Subject;
        removed.Id.Should().Be(link.Id);
        removed.Name.Should().Be("Charter");
        removed.Url.Should().Be("https://example.com/charter");
        removed.OwnerId.Should().Be(_ownerId);
    }
}
