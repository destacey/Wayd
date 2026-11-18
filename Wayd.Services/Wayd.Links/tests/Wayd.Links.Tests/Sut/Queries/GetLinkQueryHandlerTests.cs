using Wayd.Links.Queries;
using Wayd.Links.Tests.Infrastructure;

namespace Wayd.Links.Tests.Sut.Queries;

public sealed class GetLinkQueryHandlerTests
{
    private readonly LinkTestHarness _harness = new();

    private GetLinkQueryHandler CreateHandler() => new(_harness.Resolver(), _harness.DbContext);

    [Fact]
    public async Task Handle_ShouldReturnTheLink()
    {
        // Arrange
        var link = _harness.AddLink();

        // Act
        var result = await CreateHandler().Handle(new GetLinkQuery(LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value!.Id.Should().Be(link.Id);
        result.Value.Name.Should().Be("Charter");
        result.Value.Url.Should().Be("https://example.com/charter");
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_ForALinkOnAnotherRecord()
    {
        // Arrange
        var link = _harness.AddLink(ownerId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetLinkQuery(LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheRecordCannotBeRead()
    {
        // Arrange
        var link = _harness.AddLink();
        _harness.OwnerHidden();

        // Act
        var result = await CreateHandler().Handle(new GetLinkQuery(LinkTestHarness.Gadget.Key, _harness.OwnerId, link.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeNull();
    }
}
