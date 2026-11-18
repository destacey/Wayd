using Wayd.Common.Domain.Links;
using Wayd.Links.Queries;
using Wayd.Links.Tests.Infrastructure;

namespace Wayd.Links.Tests.Sut.Queries;

public sealed class GetLinksQueryHandlerTests
{
    private readonly LinkTestHarness _harness = new();

    private GetLinksQueryHandler CreateHandler() => new(_harness.Resolver(), _harness.DbContext);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ShouldReturnTheRecordsLinksByName_AndWhetherTheCallerMayManageThem(bool canManage)
    {
        // Arrange
        _harness.Owner(canManage);
        _harness.AddLink(name: "Wiki", url: "https://example.com/wiki");
        _harness.AddLink(name: "Charter", url: "https://example.com/charter");
        _harness.AddLink(name: "Elsewhere", ownerId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetLinksQuery(LinkTestHarness.Gadget.Key, _harness.OwnerId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value!.CanManage.Should().Be(canManage);
        result.Value.Links.Select(l => l.Name).Should().Equal("Charter", "Wiki");
        result.Value.Links.Should().OnlyContain(l => l.OwnerType == LinkTestHarness.Gadget.Key && l.OwnerId == _harness.OwnerId);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheRecordCannotBeRead()
    {
        // Arrange
        _harness.AddLink();
        _harness.OwnerHidden();

        // Act
        var result = await CreateHandler().Handle(new GetLinksQuery(LinkTestHarness.Gadget.Key, _harness.OwnerId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnUnregisteredOwnerType()
    {
        // Arrange
        const string key = "test.link-queries.unregistered";

        // Act
        var result = await CreateHandler().Handle(new GetLinksQuery(key, _harness.OwnerId), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkErrors.UnknownOwnerType(key));
    }
}
