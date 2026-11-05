using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>The sync retiring a record type its source has dropped.</summary>
public sealed class RetireManagedRecordTypeCommandHandlerTests : StatusWorkflowHandlerTestBase
{
    private static readonly WorkflowOwnerDescriptor Synced = new(
        "test.retire.recordtype", "Item", new Dictionary<int, WorkflowAlias>(), [], hasRecordTypes: true, hasManagedWorkflows: true);

    public RetireManagedRecordTypeCommandHandlerTests() => WorkflowOwners.Register(Synced);

    private RetireManagedRecordTypeCommandHandler CreateSut() =>
        new(DbContext, DateTimeProvider.Object, Logger<RetireManagedRecordTypeCommandHandler>());

    [Fact]
    public async Task Handle_ShouldRetireTheType()
    {
        // Arrange
        var recordType = RecordType.CreateManaged(Synced.Key, "Bug", "conn-1:bug", EventActor.Sync(null), Now).Value;
        DbContext.AddRecordType(recordType);

        // Act
        var result = await CreateSut().Handle(new RetireManagedRecordTypeCommand(Synced.Key, "conn-1:bug"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        recordType.IsActive.Should().BeFalse();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnedTypeWithoutThatKey()
    {
        // Arrange
        var owned = RecordType.Create(Synced.Key, "Bug", EventActor.System, Now).Value;
        DbContext.AddRecordType(owned);

        // Act
        var result = await CreateSut().Handle(new RetireManagedRecordTypeCommand(Synced.Key, "conn-1:bug"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Record type not found.");
        owned.IsActive.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
