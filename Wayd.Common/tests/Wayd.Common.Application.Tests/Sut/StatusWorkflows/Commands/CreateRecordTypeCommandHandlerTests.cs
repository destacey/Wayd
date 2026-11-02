using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>Defining a record type.</summary>
public sealed class CreateRecordTypeCommandHandlerTests : WorkflowSchemeHandlerTestBase
{
    private CreateRecordTypeCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<CreateRecordTypeCommandHandler>(), OwnerAvailability);

    [Fact]
    public async Task Handle_ShouldDefineTheType()
    {
        // Act
        var result = await CreateSut().Handle(new CreateRecordTypeCommand(Ticket.Key, "Spike"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        DbContext.RecordTypes.Single(r => r.Id == result.Value).Name.Should().Be("Spike");
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldBeRefused_WhenTheNameIsTaken()
    {
        // Act
        var result = await CreateSut().Handle(new CreateRecordTypeCommand(Ticket.Key, " Bug "), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A record type named 'Bug' already exists.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeRefused_ForAnOwnerTypeWithoutRecordTypes()
    {
        // Act
        var result = await CreateSut().Handle(new CreateRecordTypeCommand(Widget.Key, "Spike"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
