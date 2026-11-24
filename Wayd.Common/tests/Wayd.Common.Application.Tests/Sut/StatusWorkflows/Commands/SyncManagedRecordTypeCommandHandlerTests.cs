using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared.Extensions;
using Wayd.Tests.Shared.Infrastructure;

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

    /// <summary>
    /// A managed type as loaded after earlier syncs saved and then retired it: it has its
    /// <see cref="RecordType.Key"/>, so its events go straight to <see cref="RecordType.DomainEvents"/>,
    /// and none are pending.
    /// </summary>
    private RecordType SeedRetiredAsSaved(string externalKey, string name, int key)
    {
        var recordType = SeedManaged(externalKey, name);
        recordType.SetPrivate(r => r.Key, key);
        recordType.ClearPostPersistenceActions();
        recordType.RetireFromSource(EventActor.Sync(null), Now);
        recordType.ClearDomainEvents();

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

    [Fact]
    public async Task Handle_ShouldLeaveTheTypeUnchanged_WhenTheNewNameIsTaken()
    {
        // Arrange
        SeedManaged("conn-2:defect", "Defect");
        var recordType = SeedRetiredAsSaved("conn-1:bug", "Bug", key: 7);

        // Act
        var result = await CreateSut().Handle(new SyncManagedRecordTypeCommand(Synced.Key, "conn-1:bug", " Defect "), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A record type named 'Defect' already exists.");
        (recordType.Name, recordType.IsActive).Should().Be(("Bug", false));
        recordType.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheTypeRetired_WhenAnotherActiveTypeTookItsNameMeanwhile()
    {
        // Arrange — a retired type does not hold its name, so another took it before the source brought this one back
        var recordType = SeedRetiredAsSaved("conn-1:bug", "Bug", key: 7);
        SeedManaged("conn-2:bug", "Bug");

        // Act
        var result = await CreateSut().Handle(new SyncManagedRecordTypeCommand(Synced.Key, "conn-1:bug", "Bug"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A record type named 'Bug' already exists.");
        recordType.IsActive.Should().BeFalse();
        recordType.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFindTheNameTaken_WhenTheOwnerTypeIsPassedInAnotherForm()
    {
        // Arrange
        SeedManaged("conn-2:bug", "Bug");

        // Act
        var result = await CreateSut().Handle(new SyncManagedRecordTypeCommand($" {Synced.Key.ToUpperInvariant()} ", "conn-1:bug", "Bug"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A record type named 'Bug' already exists.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldExplainACollision_WhenTheSaveFailsAtTheDatabase()
    {
        // Arrange — another sync of the source created the same type first
        DbContext.FailNextSaveWith = SaveFailures.UniqueKey();

        // Act
        var result = await CreateSut().Handle(new SyncManagedRecordTypeCommand(Synced.Key, "conn-1:bug", "Bug"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Unable to save the managed record type 'conn-1:bug', most likely because another sync of the same source saved it at the same time. The next sync picks it up.");
        DbContext.SaveChangesCallCount.Should().Be(1, "the one save was the one that failed");
    }

    [Fact]
    public async Task Handle_ShouldNotClaimACollision_WhenTheSaveFailsForAnotherReason()
    {
        // Arrange — a value too long for its column fails every sync, so it must not read as a race the next run fixes
        DbContext.FailNextSaveWith = SaveFailures.Truncation();

        // Act
        var act = () => CreateSut().Handle(new SyncManagedRecordTypeCommand(Synced.Key, "conn-1:bug", "Bug"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }
}
