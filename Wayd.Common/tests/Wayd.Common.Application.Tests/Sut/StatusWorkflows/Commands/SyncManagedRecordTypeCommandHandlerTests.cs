using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>The sync keeping a managed record type in line with its source.</summary>
public sealed class SyncManagedRecordTypeCommandHandlerTests : StatusWorkflowHandlerTestBase
{
    private static readonly WorkflowOwnerDescriptor Synced = new(
        "test.sync.recordtype", "Item", new Dictionary<int, WorkflowAlias>(), [], hasRecordTypes: true, hasManagedWorkflows: true);

    public SyncManagedRecordTypeCommandHandlerTests() => WorkflowOwners.Register(Synced);

    private SyncManagedRecordTypeCommandHandler CreateSut() =>
        new(DbContext, DateTimeProvider.Object, Logger<SyncManagedRecordTypeCommandHandler>());

    private RecordType SeedManaged(string externalKey, string name)
    {
        var recordType = RecordType.CreateManaged(Synced.Key, name, externalKey, EventActor.Sync(null), Now).Value;
        DbContext.AddRecordType(recordType);

        return recordType;
    }

    [Fact]
    public async Task Handle_ShouldCreateAManagedType_TheFirstTime()
    {
        // Act
        var result = await CreateSut().Handle(new SyncManagedRecordTypeCommand(Synced.Key, "conn-1:bug", "Bug"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var recordType = DbContext.RecordTypes.Single(r => r.Id == result.Value);
        (recordType.Name, recordType.Ownership, recordType.ExternalKey).Should().Be(("Bug", WorkflowOwnership.Managed, "conn-1:bug"));
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRenameAndReinstateTheExistingType()
    {
        // Arrange
        var recordType = SeedManaged("conn-1:bug", "Bug");
        recordType.RetireFromSource(EventActor.Sync(null), Now);

        // Act
        var result = await CreateSut().Handle(new SyncManagedRecordTypeCommand(Synced.Key, "conn-1:bug", "Defect"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(recordType.Id);
        (recordType.Name, recordType.IsActive).Should().Be(("Defect", true));
        DbContext.RecordTypes.Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCreateTheType_WhenOnlyARetiredTypeHasTheName()
    {
        // Arrange
        var retired = SeedManaged("conn-1:bug", "Bug");
        retired.RetireFromSource(EventActor.Sync(null), Now);

        // Act
        var result = await CreateSut().Handle(new SyncManagedRecordTypeCommand(Synced.Key, "conn-1:bug-2", "Bug"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().NotBe(retired.Id);
        DbContext.RecordTypes.Should().HaveCount(2);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAnotherTypeHasTheName()
    {
        // Arrange
        SeedManaged("conn-2:bug", "Bug");

        // Act
        var result = await CreateSut().Handle(new SyncManagedRecordTypeCommand(Synced.Key, "conn-1:bug", "Bug"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A record type named 'Bug' already exists.");
        DbContext.RecordTypes.Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
