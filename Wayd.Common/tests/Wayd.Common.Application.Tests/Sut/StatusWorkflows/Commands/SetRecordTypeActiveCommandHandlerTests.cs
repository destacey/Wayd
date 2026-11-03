using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>Activating and deactivating a record type.</summary>
public sealed class SetRecordTypeActiveCommandHandlerTests : WorkflowSchemeHandlerTestBase
{
    private SetRecordTypeActiveCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<SetRecordTypeActiveCommandHandler>(), OwnerAvailability);

    [Fact]
    public async Task Handle_ShouldReactivateTheType_WhenItsNameIsFree()
    {
        // Arrange
        Bug.Deactivate(EventActor.System, Now);

        // Act
        var result = await CreateSut().Handle(new SetRecordTypeActiveCommand(Bug.Id, true), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        Bug.IsActive.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseToReactivate_WhenAnActiveTypeHasTakenTheName()
    {
        // Arrange
        Bug.Deactivate(EventActor.System, Now);
        DbContext.AddRecordType(RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value);

        // Act
        var result = await CreateSut().Handle(new SetRecordTypeActiveCommand(Bug.Id, true), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Another active record type is named 'Bug'. Rename one of them first.");
        Bug.IsActive.Should().BeFalse();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
