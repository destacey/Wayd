using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Models;

namespace Wayd.Common.Domain.Tests.Sut.Models;

public sealed class OwnershipInfoTests
{
    [Fact]
    public void CreateWaydOwned_ShouldReturnOwned_WithNoConnectorOrIds()
    {
        var info = OwnershipInfo.CreateWaydOwned();

        info.Should().NotBeNull();
        info.Ownership.Should().Be(Ownership.Owned);
        info.Connector.Should().BeNull();
        info.SystemId.Should().BeNull();
        info.ExternalId.Should().BeNull();
    }

    [Fact]
    public void CreateSystemOwned_ShouldReturnSystem_WithNoConnectorOrIds()
    {
        var info = OwnershipInfo.CreateSystemOwned();

        info.Should().NotBeNull();
        info.Ownership.Should().Be(Ownership.System);
        info.Connector.Should().BeNull();
        info.SystemId.Should().BeNull();
        info.ExternalId.Should().BeNull();
    }

    [Fact]
    public void CreateExternalOwned_ShouldReturnManaged_AndTrimIds()
    {
        var connector = Connector.AzureDevOps;
        var systemId = " my-system ";
        var externalId = " ext-123 ";

        var info = OwnershipInfo.CreateExternalOwned(connector, systemId, externalId);

        info.Should().NotBeNull();
        info.Ownership.Should().Be(Ownership.Managed);
        info.Connector.Should().Be(connector);
        info.SystemId.Should().Be("my-system");
        info.ExternalId.Should().Be("ext-123");
    }

    [Fact]
    public void CreateExternalOwned_ShouldReturnManaged_AndTrimIds_WhenNoSystemId()
    {
        var connector = Connector.AzureDevOps;
        string? systemId = null;
        var externalId = " ext-123 ";

        var info = OwnershipInfo.CreateExternalOwned(connector, systemId, externalId);

        info.Should().NotBeNull();
        info.Ownership.Should().Be(Ownership.Managed);
        info.Connector.Should().Be(connector);
        info.SystemId.Should().Be(systemId);
        info.ExternalId.Should().Be(externalId.Trim());
    }

    [Fact]
    public void CreateExternalOwned_ShouldThrow_WhenExternalIdIsWhitespace()
    {
        var connector = Connector.AzureDevOps;
        var systemId = "sys";
        var externalId = " ";

        FluentActions.Invoking(() => OwnershipInfo.CreateExternalOwned(connector, systemId, externalId))
        .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OwnedInstances_ShouldBeEqual()
    {
        var a = OwnershipInfo.CreateWaydOwned();
        var b = OwnershipInfo.CreateWaydOwned();

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void GetEqualityComponents_WithSameIds_ShouldBeEqual()
    {
        var connector = Connector.AzureDevOps;
        var systemId = "sys1";
        var externalId = "ext1";

        var a = OwnershipInfo.CreateExternalOwned(connector, systemId, externalId);
        var b = OwnershipInfo.CreateExternalOwned(connector, systemId, externalId);

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void GetEqualityComponents_WithDifferentIds_ShouldNotBeEqual()
    {
        var connector = Connector.AzureDevOps;

        var a = OwnershipInfo.CreateExternalOwned(connector, "sys1", "ext1");
        var b = OwnershipInfo.CreateExternalOwned(connector, "sys2", "ext2");

        a.Equals(b).Should().BeFalse();
        (a == b).Should().BeFalse();
    }
}
