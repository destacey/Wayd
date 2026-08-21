using Wayd.Work.Domain.Models;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public sealed class WorkItemExtendedTests
{
    private const string IterationPath = "Contoso\\Sprint 14";
    private const string AssignedTo = "6f2a0c94-e5b8-4d17-9a63-2c8e1b74f052";
    private const string CreatedBy = "8d1e5c70-3a84-4b29-9f61-2c7e0a53d918";

    private static readonly Guid _workItemId = Guid.Parse("1c9f0a3e-6b4d-4f2a-9c81-5d0e7a2b4f63");

    [Fact]
    public void Create_ReturnsNull_WhenNothingWasReported()
    {
        // Arrange & Act
        var extended = WorkItemExtended.Create(_workItemId, null);

        // Assert
        extended.Should().BeNull();
    }

    [Fact]
    public void Create_ReturnsARow_WhenOnlyTheTeamIdentifierIsPresent()
    {
        // Arrange & Act
        var extended = WorkItemExtended.Create(_workItemId, IterationPath);

        // Assert
        extended.Should().NotBeNull();
        extended!.ExternalTeamIdentifier.Should().Be(IterationPath);
    }

    // The row used to exist only for the iteration path. Keying on it alone would drop the
    // identity ids for any item without one, and WorkItem.Update clears ExtendedProps outright
    // when handed null - so those ids would vanish on the next sync.
    [Fact]
    public void Create_ReturnsARow_WhenOnlyAnExternalIdentityIsPresent()
    {
        // Arrange & Act
        var extended = WorkItemExtended.Create(_workItemId, null, assignedToExternalId: AssignedTo);

        // Assert
        extended.Should().NotBeNull();
        extended!.AssignedToExternalId.Should().Be(AssignedTo);
        extended.ExternalTeamIdentifier.Should().BeNull();
    }

    [Fact]
    public void Create_KeepsEveryReportedIdentity()
    {
        // Arrange & Act
        var extended = WorkItemExtended.Create(
            _workItemId,
            IterationPath,
            assignedToExternalId: AssignedTo,
            createdByExternalId: CreatedBy,
            lastModifiedByExternalId: CreatedBy);

        // Assert
        extended.Should().NotBeNull();
        extended!.AssignedToExternalId.Should().Be(AssignedTo);
        extended.CreatedByExternalId.Should().Be(CreatedBy);
        extended.LastModifiedByExternalId.Should().Be(CreatedBy);
    }

    [Fact]
    public void Update_CarriesEveryFieldFromTheIncomingRow()
    {
        // Arrange
        var extended = WorkItemExtended.Create(_workItemId, IterationPath)!;
        var incoming = WorkItemExtended.Create(
            _workItemId,
            "Contoso\\Sprint 15",
            assignedToExternalId: AssignedTo,
            createdByExternalId: CreatedBy,
            lastModifiedByExternalId: CreatedBy);

        // Act
        extended.Update(incoming);

        // Assert
        extended.ExternalTeamIdentifier.Should().Be("Contoso\\Sprint 15");
        extended.AssignedToExternalId.Should().Be(AssignedTo);
        extended.CreatedByExternalId.Should().Be(CreatedBy);
        extended.LastModifiedByExternalId.Should().Be(CreatedBy);
    }

    [Fact]
    public void Update_ClearsEveryField_WhenTheSourceReportsNothing()
    {
        // Arrange
        var extended = WorkItemExtended.Create(
            _workItemId,
            IterationPath,
            assignedToExternalId: AssignedTo)!;

        // Act
        extended.Update(null);

        // Assert
        extended.ExternalTeamIdentifier.Should().BeNull();
        extended.AssignedToExternalId.Should().BeNull();
    }
}
