using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.AppIntegration;
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
        ExternalIdentityMapping.CreateUnmapped(Connector.AzureDevOps, _connectionId, externalId, email, "Avery Chen", email, EventActor.System, _seen);

    private static ExternalIdentityMapping CreateAutoMatched(string externalId = IdentityGuid, string? email = WorkAddress) =>
        ExternalIdentityMapping.CreateAutoMatched(Connector.AzureDevOps, _connectionId, externalId, email, "Avery Chen", email, _employeeId, EventActor.System, _seen);

    /// <summary>A mapping with its creation event cleared, so a test sees only what its act raised.</summary>
    private static ExternalIdentityMapping Settled(ExternalIdentityMapping mapping)
    {
        mapping.ClearDomainEvents();
        return mapping;
    }

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
    public void CreateUnmapped_RaisesCreated_WithoutAnEmployee()
    {
        // Arrange & Act
        var mapping = CreateUnmapped();

        // Assert
        var created = mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingCreatedEvent>().Subject;
        created.Id.Should().Be(mapping.Id);
        created.Connector.Should().Be(Connector.AzureDevOps);
        created.ConnectionId.Should().Be(_connectionId);
        created.ExternalId.Should().Be(IdentityGuid);
        created.EmployeeId.Should().BeNull();
        created.Status.Should().Be(ExternalIdentityMappingStatus.Unmapped);
        created.Actor.Should().Be(EventActor.System);
        created.Timestamp.Should().Be(_seen);
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
    public void CreateAutoMatched_RaisesCreated_WithTheMatchedEmployee()
    {
        // Arrange & Act
        var mapping = CreateAutoMatched();

        // Assert
        var created = mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingCreatedEvent>().Subject;
        created.Id.Should().Be(mapping.Id);
        created.EmployeeId.Should().Be(_employeeId);
        created.Status.Should().Be(ExternalIdentityMappingStatus.AutoMatched);
        created.Timestamp.Should().Be(_seen);
    }

    [Fact]
    public void RefreshFromSync_RepointsAnAutoMatchedRow()
    {
        // Arrange
        var mapping = CreateAutoMatched();

        // Act
        mapping.RefreshFromSync(NewAddress, "Avery Chen-Okafor", NewAddress, _otherEmployeeId, EventActor.System, _later);

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
        mapping.RefreshFromSync(WorkAddress, "Avery Chen", WorkAddress, null, EventActor.System, _later);

        // Assert
        mapping.EmployeeId.Should().BeNull();
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.Unmapped);
    }

    [Fact]
    public void RefreshFromSync_PreservesAManualMapping()
    {
        // Arrange
        var mapping = CreateUnmapped();
        mapping.MapToEmployee(_employeeId, EventActor.System, _seen);

        // Act — the sync would have auto-matched a different employee
        mapping.RefreshFromSync(NewAddress, "Avery Chen", NewAddress, _otherEmployeeId, EventActor.System, _later);

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
        mapping.Ignore(EventActor.System, _seen);

        // Act
        mapping.RefreshFromSync(WorkAddress, "Build Service", WorkAddress, _employeeId, EventActor.System, _later);

        // Assert
        mapping.EmployeeId.Should().BeNull();
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.Ignored);
    }

    [Fact]
    public void RefreshFromSync_RaisesNothing_WhenOnlyLastSeenMoves()
    {
        // Arrange
        var mapping = Settled(CreateAutoMatched());

        // Act — the trimmed fields arrive padded.
        mapping.RefreshFromSync($" {WorkAddress} ", "Avery Chen  ", WorkAddress, _employeeId, EventActor.System, _later);

        // Assert
        mapping.DomainEvents.Should().BeEmpty();
        mapping.LastSeen.Should().Be(_later);
    }

    [Fact]
    public void RefreshFromSync_RaisesNothing_WhenAnUnmappedRowStaysUnresolved()
    {
        // Arrange
        var mapping = Settled(CreateUnmapped());

        // Act
        mapping.RefreshFromSync(WorkAddress, "Avery Chen", WorkAddress, null, EventActor.System, _later);

        // Assert
        mapping.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RefreshFromSync_RaisesProfileChanged_WhenTheDisplayNameChanges()
    {
        // Arrange
        var mapping = Settled(CreateAutoMatched());

        // Act
        mapping.RefreshFromSync(WorkAddress, "Avery Chen-Okafor", WorkAddress, _employeeId, EventActor.System, _later);

        // Assert
        var changed = mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingProfileChangedEvent>().Subject;
        changed.Id.Should().Be(mapping.Id);
        changed.Timestamp.Should().Be(_later);
    }

    [Fact]
    public void RefreshFromSync_RaisesNoResolutionEvent_OnAnAdminDecidedRow()
    {
        // Arrange
        var mapping = CreateUnmapped();
        mapping.MapToEmployee(_employeeId, EventActor.System, _seen);
        mapping.ClearDomainEvents();

        // Act — a different match and a changed profile: only the profile may move.
        mapping.RefreshFromSync(NewAddress, "Avery Chen", NewAddress, _otherEmployeeId, EventActor.System, _later);

        // Assert
        mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingProfileChangedEvent>();
    }

    [Fact]
    public void RefreshFromSync_RaisesAutoMatched_WhenMatchedToADifferentEmployee()
    {
        // Arrange
        var mapping = Settled(CreateAutoMatched());

        // Act
        mapping.RefreshFromSync(WorkAddress, "Avery Chen", WorkAddress, _otherEmployeeId, EventActor.System, _later);

        // Assert
        var matched = mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingAutoMatchedEvent>().Subject;
        matched.Id.Should().Be(mapping.Id);
        matched.PreviousStatus.Should().Be(ExternalIdentityMappingStatus.AutoMatched);
        matched.PreviousEmployeeId.Should().Be(_employeeId);
        matched.EmployeeId.Should().Be(_otherEmployeeId);
        matched.Timestamp.Should().Be(_later);
    }

    [Fact]
    public void RefreshFromSync_RaisesAutoMatched_WhenAnUnmappedRowStartsResolving()
    {
        // Arrange
        var mapping = Settled(CreateUnmapped());

        // Act
        mapping.RefreshFromSync(WorkAddress, "Avery Chen", WorkAddress, _employeeId, EventActor.System, _later);

        // Assert
        var matched = mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingAutoMatchedEvent>().Subject;
        matched.PreviousStatus.Should().Be(ExternalIdentityMappingStatus.Unmapped);
        matched.PreviousEmployeeId.Should().BeNull();
        matched.EmployeeId.Should().Be(_employeeId);
    }

    [Fact]
    public void RefreshFromSync_RaisesUnmatched_WhenTheMatchIsLost()
    {
        // Arrange
        var mapping = Settled(CreateAutoMatched());

        // Act
        mapping.RefreshFromSync(WorkAddress, "Avery Chen", WorkAddress, null, EventActor.System, _later);

        // Assert
        var unmatched = mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingUnmatchedEvent>().Subject;
        unmatched.Id.Should().Be(mapping.Id);
        unmatched.PreviousStatus.Should().Be(ExternalIdentityMappingStatus.AutoMatched);
        unmatched.PreviousEmployeeId.Should().Be(_employeeId);
        unmatched.Timestamp.Should().Be(_later);
    }

    [Fact]
    public void MapToEmployee_RejectsAnEmptyEmployee()
    {
        // Arrange
        var mapping = Settled(CreateUnmapped());

        // Act
        var result = mapping.MapToEmployee(Guid.Empty, EventActor.System, _later);

        // Assert
        result.IsFailure.Should().BeTrue();
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.Unmapped);
        mapping.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MapToEmployee_RaisesMapped_WithBothEnds()
    {
        // Arrange
        var mapping = Settled(CreateAutoMatched());

        // Act
        var result = mapping.MapToEmployee(_otherEmployeeId, EventActor.System, _later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var mapped = mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingMappedEvent>().Subject;
        mapped.Id.Should().Be(mapping.Id);
        mapped.PreviousStatus.Should().Be(ExternalIdentityMappingStatus.AutoMatched);
        mapped.PreviousEmployeeId.Should().Be(_employeeId);
        mapped.EmployeeId.Should().Be(_otherEmployeeId);
        mapped.Timestamp.Should().Be(_later);
    }

    [Fact]
    public void MapToEmployee_RaisesOnce_WhenMappedToTheSameEmployeeTwice()
    {
        // Arrange
        var mapping = Settled(CreateUnmapped());

        // Act
        mapping.MapToEmployee(_employeeId, EventActor.System, _seen);
        var result = mapping.MapToEmployee(_employeeId, EventActor.System, _later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingMappedEvent>();
    }

    [Fact]
    public void Ignore_RaisesIgnored_WithThePreviousResolution()
    {
        // Arrange
        var mapping = Settled(CreateAutoMatched());

        // Act
        mapping.Ignore(EventActor.System, _later);

        // Assert
        var ignored = mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingIgnoredEvent>().Subject;
        ignored.Id.Should().Be(mapping.Id);
        ignored.PreviousStatus.Should().Be(ExternalIdentityMappingStatus.AutoMatched);
        ignored.PreviousEmployeeId.Should().Be(_employeeId);
        ignored.Timestamp.Should().Be(_later);
    }

    [Fact]
    public void Ignore_RaisesNothing_WhenAlreadyIgnored()
    {
        // Arrange
        var mapping = CreateUnmapped();
        mapping.Ignore(EventActor.System, _seen);
        mapping.ClearDomainEvents();

        // Act
        mapping.Ignore(EventActor.System, _later);

        // Assert
        mapping.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDecision_ReturnsTheRowToTheQueue()
    {
        // Arrange
        var mapping = CreateUnmapped();
        mapping.Ignore(EventActor.System, _seen);

        // Act
        mapping.ClearDecision(EventActor.System, _later);

        // Assert
        mapping.Status.Should().Be(ExternalIdentityMappingStatus.Unmapped);
        mapping.IsAdminDecided.Should().BeFalse();
    }

    [Fact]
    public void ClearDecision_RaisesDecisionCleared_WithThePreviousResolution()
    {
        // Arrange
        var mapping = CreateUnmapped();
        mapping.MapToEmployee(_employeeId, EventActor.System, _seen);
        mapping.ClearDomainEvents();

        // Act
        mapping.ClearDecision(EventActor.System, _later);

        // Assert
        var cleared = mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingDecisionClearedEvent>().Subject;
        cleared.Id.Should().Be(mapping.Id);
        cleared.PreviousStatus.Should().Be(ExternalIdentityMappingStatus.ManuallyMapped);
        cleared.PreviousEmployeeId.Should().Be(_employeeId);
        cleared.Timestamp.Should().Be(_later);
    }

    [Fact]
    public void ClearDecision_RaisesNothing_WhenAlreadyUnmapped()
    {
        // Arrange
        var mapping = Settled(CreateUnmapped());

        // Act
        mapping.ClearDecision(EventActor.System, _later);

        // Assert
        mapping.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void TryAdoptExternalId_ReKeysASeededPlaceholder()
    {
        // Arrange — the seed migration keys rows on the address, having no identity id to use.
        var mapping = CreateAutoMatched(externalId: WorkAddress);

        // Act
        var adopted = mapping.TryAdoptExternalId(IdentityGuid, EventActor.System, _later);

        // Assert
        adopted.Should().BeTrue();
        mapping.ExternalId.Should().Be(IdentityGuid);
        mapping.EmployeeId.Should().Be(_employeeId);
    }

    [Fact]
    public void TryAdoptExternalId_RaisesRekeyed_WhenItReKeys()
    {
        // Arrange
        var mapping = Settled(CreateAutoMatched(externalId: WorkAddress));

        // Act
        mapping.TryAdoptExternalId(IdentityGuid, EventActor.System, _later);

        // Assert
        var rekeyed = mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalIdentityMappingRekeyedEvent>().Subject;
        rekeyed.Id.Should().Be(mapping.Id);
        rekeyed.ExternalId.Should().Be(IdentityGuid);
        rekeyed.Timestamp.Should().Be(_later);
    }

    [Fact]
    public void TryAdoptExternalId_RefusesToRewriteARealIdentity()
    {
        // Arrange
        var mapping = Settled(CreateAutoMatched());

        // Act
        var adopted = mapping.TryAdoptExternalId("a-different-identity", EventActor.System, _later);

        // Assert
        adopted.Should().BeFalse();
        mapping.ExternalId.Should().Be(IdentityGuid);
        mapping.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void TryAdoptExternalId_IsIdempotent()
    {
        // Arrange
        var mapping = Settled(CreateAutoMatched());

        // Act
        var adopted = mapping.TryAdoptExternalId(IdentityGuid, EventActor.System, _later);

        // Assert
        adopted.Should().BeTrue();
        mapping.ExternalId.Should().Be(IdentityGuid);
        mapping.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void TryAdoptExternalId_RefusesWhenThereIsNoAddressToProveAPlaceholder()
    {
        // Arrange
        var mapping = Settled(CreateUnmapped(externalId: WorkAddress, email: null));

        // Act
        var adopted = mapping.TryAdoptExternalId(IdentityGuid, EventActor.System, _later);

        // Assert
        adopted.Should().BeFalse();
        mapping.ExternalId.Should().Be(WorkAddress);
        mapping.DomainEvents.Should().BeEmpty();
    }
}
