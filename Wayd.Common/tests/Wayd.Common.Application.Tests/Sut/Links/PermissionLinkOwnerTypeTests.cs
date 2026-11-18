using FluentAssertions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;

namespace Wayd.Common.Application.Tests.Sut.Links;

public sealed class PermissionLinkOwnerTypeTests
{
    private const string View = "Permissions.Gadgets.View";
    private const string Update = "Permissions.Gadgets.Update";

    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _gadgetId = Guid.NewGuid();

    private GadgetLinkOwnerType CreateSut() => new(_currentPrincipal.Object, _gadgetId);

    private void Holds(string permission, bool holds) =>
        _currentPrincipal.Setup(p => p.HoldsPermission(permission, It.IsAny<CancellationToken>())).ReturnsAsync(holds);

    private void MayUse(string permission, bool may) =>
        _currentPrincipal.Setup(p => p.HasPermission(permission, It.IsAny<CancellationToken>())).ReturnsAsync(may);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolve_ShouldLetAReaderManage_OnlyWithThePermissionThatEditsTheRecord(bool mayUpdate)
    {
        // Arrange
        Holds(View, true);
        MayUse(Update, mayUpdate);

        // Act
        var access = await CreateSut().Resolve(_gadgetId, TestContext.Current.CancellationToken);

        // Assert
        access.Should().Be(new LinkOwnerAccess(mayUpdate));
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WithoutThePermissionThatReadsTheRecord()
    {
        // Arrange
        Holds(View, false);
        MayUse(Update, true);

        // Act
        var access = await CreateSut().Resolve(_gadgetId, TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheRecordDoesNotExist()
    {
        // Arrange
        Holds(View, true);
        MayUse(Update, true);

        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldReadWithTheUsersOwnPermission_SoAReadOnlyTokenStillReads()
    {
        // Arrange — the token's scope refuses every permission, but the user holds View
        Holds(View, true);
        MayUse(View, false);
        MayUse(Update, false);

        // Act
        var access = await CreateSut().Resolve(_gadgetId, TestContext.Current.CancellationToken);

        // Assert
        access.Should().Be(new LinkOwnerAccess(false));
    }

    private sealed class GadgetLinkOwnerType(ICurrentPrincipal currentPrincipal, Guid existingId)
        : PermissionLinkOwnerType(currentPrincipal)
    {
        public override string OwnerType => "test.permission-links.gadget";

        protected override string ViewPermission => View;

        protected override string ManagePermission => Update;

        protected override Task<bool> Exists(Guid ownerId, CancellationToken cancellationToken) =>
            Task.FromResult(ownerId == existingId);
    }
}
