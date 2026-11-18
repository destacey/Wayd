using Wayd.Common.Domain.Links;
using Wayd.Links.Tests.Infrastructure;

namespace Wayd.Links.Tests.Sut;

public sealed class LinkOwnerAccessResolverTests
{
    private readonly LinkTestHarness _harness = new();

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task Resolve_ShouldTakeTheOwnerTypesAnswer_NarrowedByAReadOnlyToken(bool ownerAllows, bool readOnly, bool expected)
    {
        // Arrange
        _harness.Owner(ownerAllows);
        _harness.CurrentPrincipal.SetupGet(p => p.IsReadOnly).Returns(readOnly);

        // Act
        var result = await _harness.Resolver().Resolve(" TEST.Link-Handlers.Gadget ", _harness.OwnerId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(new ResolvedLinkOwner(LinkTestHarness.Gadget.Key, _harness.OwnerId, expected));
    }

    [Fact]
    public async Task Resolve_ShouldReturnNull_WhenTheOwnerTypeHidesTheRecord()
    {
        // Arrange
        _harness.OwnerHidden();

        // Act
        var result = await _harness.Resolver().Resolve(LinkTestHarness.Gadget.Key, _harness.OwnerId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldFail_ForAnOwnerTypeNoModuleRegistered()
    {
        // Arrange
        const string key = "test.link-access.unregistered";

        // Act
        var result = await _harness.Resolver().Resolve(key, _harness.OwnerId, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkErrors.UnknownOwnerType(key));
    }

    [Fact]
    public async Task Resolve_ShouldFail_ForARegisteredOwnerTypeNoImplementationAnswersFor()
    {
        // Arrange
        var orphan = new LinkOwnerTypeDescriptor("test.link-access.no-implementation", "Orphan", "Orphan");
        LinkOwnerTypes.Register(orphan);

        // Act
        var result = await _harness.Resolver().Resolve(orphan.Key, _harness.OwnerId, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkErrors.UnknownOwnerType(orphan.Key));
    }

    [Fact]
    public async Task ForManaging_ShouldBeNotFound_WhenTheRecordIsHidden()
    {
        // Arrange
        _harness.OwnerHidden();

        // Act
        var result = await _harness.Resolver().ForManaging(LinkTestHarness.Gadget.Key, _harness.OwnerId, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.NotFound);
    }

    [Fact]
    public async Task ForManaging_ShouldBeForbidden_WhenTheRecordCanOnlyBeRead()
    {
        // Arrange
        _harness.Owner(canManage: false);

        // Act
        var result = await _harness.Resolver().ForManaging(LinkTestHarness.Gadget.Key, _harness.OwnerId, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(LinkAccessErrors.CannotManage);
    }
}
