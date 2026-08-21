using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Enums.AppIntegrations;
using NodaTime;

namespace Wayd.Common.Domain.Tests.Sut.AppIntegrations;

public sealed class ExternalIdentityMappingTests
{
    private const string IdentityGuid = "6f2a0c94-e5b8-4d17-9a63-2c8e1b74f052";
    private const string WorkAddress = "avery.chen@acme.example";
    private const string NewAddress = "avery.chen@acme-new.example";

    private static readonly Guid _connectionId = Guid.Parse("1c9f0a3e-6b4d-4f2a-9c81-5d0e7a2b4f63");
    private static readonly Guid _employeeId = Guid.Parse("3d9a5f71-84c2-4e60-b1d7-6f2a0c94e5b8");
    private static readonly Guid _otherEmployeeId = Guid.Parse("7b1e4c28-9f03-4a56-8d72-0e5a3c96b1d4");
    private static readonly Instant _seen = Instant.FromUtc(2026, 8, 20, 12, 0, 0);
    private static readonly Instant _later = Instant.FromUtc(2026, 8, 21, 12, 0, 0);

    private static ExternalIdentityMapping CreateUnmapped(string externalId = IdentityGuid, string? email = WorkAddress) =>
        ExternalIdentityMapping.CreateUnmapped(Connector.AzureDevOps, _connectionId, externalId, email, "Avery Chen", email, _seen);

    private static ExternalIdentityMapping CreateAutoMatched(string externalId = IdentityGuid, string? email = WorkAddress) =>
        ExternalIdentityMapping.CreateAutoMatched(Connector.AzureDevOps, _connectionId, externalId, email, "Avery Chen", email, _employeeId, _seen);

    [Fact]
    public void CreateUnmapped_LeavesEmployeeUnset()
    {
        // Arrange & Act
        var mapping = CreateUnmapped();

        // Assert
        mapping.EmployeeId.Should().BeNull();
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.Unmapped);
        mapping.IsAdminDecided.Should().BeFalse();
    }

    [Fact]
    public void CreateAutoMatched_IsNotAdminDecided()
    {
        // Arrange & Act
        var mapping = CreateAutoMatched();

        // Assert
        mapping.EmployeeId.Should().Be(_employeeId);
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.AutoMatched);
        mapping.IsAdminDecided.Should().BeFalse();
    }

    [Fact]
    public void RefreshFromSync_RepointsAnAutoMatchedRow()
    {
        // Arrange
        var mapping = CreateAutoMatched();

        // Act
        mapping.RefreshFromSync(NewAddress, "Avery Chen-Okafor", NewAddress, _otherEmployeeId, _later);

        // Assert
        mapping.EmployeeId.Should().Be(_otherEmployeeId);
        mapping.Email.Should().Be(NewAddress);
        mapping.DisplayName.Should().Be("Avery Chen-Okafor");
        mapping.LastSeen.Should().Be(_later);
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.AutoMatched);
    }

    [Fact]
    public void RefreshFromSync_ReturnsAnAutoMatchedRowToTheQueue_WhenTheAddressStopsResolving()
    {
        // Arrange
        var mapping = CreateAutoMatched();

        // Act
        mapping.RefreshFromSync(WorkAddress, "Avery Chen", WorkAddress, null, _later);

        // Assert
        mapping.EmployeeId.Should().BeNull();
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.Unmapped);
    }

    [Fact]
    public void RefreshFromSync_PreservesAManualMapping()
    {
        // Arrange
        var mapping = CreateUnmapped();
        mapping.MapToEmployee(_employeeId);

        // Act — the sync would have auto-matched a different employee
        mapping.RefreshFromSync(NewAddress, "Avery Chen", NewAddress, _otherEmployeeId, _later);

        // Assert
        mapping.EmployeeId.Should().Be(_employeeId);
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.ManuallyMapped);
        // Descriptive fields still track the source.
        mapping.Email.Should().Be(NewAddress);
        mapping.LastSeen.Should().Be(_later);
    }

    [Fact]
    public void RefreshFromSync_PreservesAnIgnoredRow()
    {
        // Arrange
        var mapping = CreateUnmapped();
        mapping.Ignore();

        // Act
        mapping.RefreshFromSync(WorkAddress, "Build Service", WorkAddress, _employeeId, _later);

        // Assert
        mapping.EmployeeId.Should().BeNull();
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.Ignored);
    }

    [Fact]
    public void MapToEmployee_RejectsAnEmptyEmployee()
    {
        // Arrange
        var mapping = CreateUnmapped();

        // Act
        var result = mapping.MapToEmployee(Guid.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.Unmapped);
    }

    [Fact]
    public void ClearDecision_ReturnsTheRowToTheQueue()
    {
        // Arrange
        var mapping = CreateUnmapped();
        mapping.Ignore();

        // Act
        mapping.ClearDecision();

        // Assert
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.Unmapped);
        mapping.IsAdminDecided.Should().BeFalse();
    }

    [Fact]
    public void TryAdoptExternalId_ReKeysASeededPlaceholder()
    {
        // Arrange — the seed migration keys rows on the address, having no identity id to use.
        var mapping = CreateAutoMatched(externalId: WorkAddress);

        // Act
        var adopted = mapping.TryAdoptExternalId(IdentityGuid);

        // Assert
        adopted.Should().BeTrue();
        mapping.ExternalId.Should().Be(IdentityGuid);
        mapping.EmployeeId.Should().Be(_employeeId);
    }

    [Fact]
    public void TryAdoptExternalId_RefusesToRewriteARealIdentity()
    {
        // Arrange
        var mapping = CreateAutoMatched();

        // Act
        var adopted = mapping.TryAdoptExternalId("a-different-identity");

        // Assert
        adopted.Should().BeFalse();
        mapping.ExternalId.Should().Be(IdentityGuid);
    }

    [Fact]
    public void TryAdoptExternalId_IsIdempotent()
    {
        // Arrange
        var mapping = CreateAutoMatched();

        // Act
        var adopted = mapping.TryAdoptExternalId(IdentityGuid);

        // Assert
        adopted.Should().BeTrue();
        mapping.ExternalId.Should().Be(IdentityGuid);
    }

    [Fact]
    public void TryAdoptExternalId_RefusesWhenThereIsNoAddressToProveAPlaceholder()
    {
        // Arrange
        var mapping = CreateUnmapped(externalId: WorkAddress, email: null);

        // Act
        var adopted = mapping.TryAdoptExternalId(IdentityGuid);

        // Assert
        adopted.Should().BeFalse();
        mapping.ExternalId.Should().Be(WorkAddress);
    }
}
