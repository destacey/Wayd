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

    [Fact]
    public void GetExternalReference_ShouldReturnTheReference_WhenManaged()
    {
        // Arrange
        var info = OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, "sys1", "ext1");

        // Act
        var reference = info.GetExternalReference();

        // Assert
        reference.Should().Be(new ExternalReference(Connector.AzureDevOps, "sys1", "ext1"));
        info.HasExternalReference.Should().BeTrue();
    }

    [Fact]
    public void GetExternalReference_ShouldReturnNull_WhenTheRecordHasNone()
    {
        // Arrange
        var owned = OwnershipInfo.CreateWaydOwned();
        var system = OwnershipInfo.CreateSystemOwned();

        // Act / Assert
        owned.GetExternalReference().Should().BeNull();
        owned.HasExternalReference.Should().BeFalse();
        system.GetExternalReference().Should().BeNull();
        system.HasExternalReference.Should().BeFalse();
    }

    [Fact]
    public void Release_ShouldReturnOwned_AndKeepTheReference()
    {
        // Arrange
        var managed = OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, "sys1", "ext1");

        // Act
        var result = managed.Release();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Ownership.Should().Be(Ownership.Owned);
        result.Value.GetExternalReference().Should().Be(new ExternalReference(Connector.AzureDevOps, "sys1", "ext1"));
        result.Value.Should().NotBe(managed);
        managed.Ownership.Should().Be(Ownership.Managed);
    }

    [Fact]
    public void Release_ShouldFail_WhenOwned()
    {
        // Arrange
        var owned = OwnershipInfo.CreateWaydOwned();

        // Act
        var result = owned.Release();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Release_ShouldFail_WhenSystem()
    {
        // Arrange
        var system = OwnershipInfo.CreateSystemOwned();

        // Act
        var result = system.Release();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Adopt_ShouldReturnManaged_WhenOwnedWithNoReference()
    {
        // Arrange
        var owned = OwnershipInfo.CreateWaydOwned();

        // Act
        var result = owned.Adopt(new ExternalReference(Connector.Workday, "tenant1", " 10482 "));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(OwnershipInfo.CreateExternalOwned(Connector.Workday, "tenant1", "10482"));
    }

    [Fact]
    public void Adopt_ShouldReturnManaged_WhenTheReferenceMatchesTheOneKeptOnRelease()
    {
        // Arrange
        var released = OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, "sys1", "ext1").Release().Value;

        // Act
        var result = released.Adopt(new ExternalReference(Connector.AzureDevOps, "SYS1", "EXT1"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Ownership.Should().Be(Ownership.Managed);
        result.Value.GetExternalReference().Should().Be(new ExternalReference(Connector.AzureDevOps, "SYS1", "EXT1"));
    }

    [Fact]
    public void Adopt_ShouldTakeTheAdoptersSystemId_WhenTheKeptReferenceHasNone()
    {
        // Arrange
        var released = OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, null, "ext1").Release().Value;

        // Act
        var result = released.Adopt(new ExternalReference(Connector.AzureDevOps, "sys1", "ext1"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SystemId.Should().Be("sys1");
    }

    [Theory]
    [InlineData(Connector.Workday, "sys1", "ext1")]
    [InlineData(Connector.AzureDevOps, "sys2", "ext1")]
    [InlineData(Connector.AzureDevOps, null, "ext1")]
    [InlineData(Connector.AzureDevOps, "sys1", "ext2")]
    public void Adopt_ShouldFail_WhenTheReferenceDiffersFromTheOneKeptOnRelease(Connector connector, string? systemId, string externalId)
    {
        // Arrange
        var released = OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, "sys1", "ext1").Release().Value;

        // Act
        var result = released.Adopt(new ExternalReference(connector, systemId, externalId));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Adopt_ShouldFail_WhenAlreadyManaged()
    {
        // Arrange
        var managed = OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, "sys1", "ext1");

        // Act
        var result = managed.Adopt(new ExternalReference(Connector.AzureDevOps, "sys1", "ext1"));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Adopt_ShouldFail_WhenSystem()
    {
        // Arrange
        var system = OwnershipInfo.CreateSystemOwned();

        // Act
        var result = system.Adopt(new ExternalReference(Connector.AzureDevOps, "sys1", "ext1"));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Adopt_ShouldFail_WhenTheExternalIdIsBlank()
    {
        // Arrange
        var owned = OwnershipInfo.CreateWaydOwned();

        // Act
        var result = owned.Adopt(new ExternalReference(Connector.AzureDevOps, "sys1", " "));

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
