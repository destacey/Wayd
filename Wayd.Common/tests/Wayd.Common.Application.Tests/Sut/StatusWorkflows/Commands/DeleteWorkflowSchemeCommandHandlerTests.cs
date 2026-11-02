using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>Deleting a workflow scheme.</summary>
public sealed class DeleteWorkflowSchemeCommandHandlerTests : WorkflowSchemeHandlerTestBase
{
    private DeleteWorkflowSchemeCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, DateTimeProvider.Object, Logger<DeleteWorkflowSchemeCommandHandler>(), OwnerAvailability);

    [Fact]
    public async Task Handle_ShouldBeRefused_WhileAScopeFollowsTheScheme()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var scheme = Scheme(standard);
        Assign(scheme, standard, Guid.NewGuid());

        // Act
        var result = await CreateSut().Handle(new DeleteWorkflowSchemeCommand(scheme.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.StatusWorkflowSchemes.Should().Contain(scheme);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldDeleteAnUnassignedScheme()
    {
        // Arrange
        var scheme = Scheme(TicketWorkflow("Standard"));

        // Act
        var result = await CreateSut().Handle(new DeleteWorkflowSchemeCommand(scheme.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        DbContext.StatusWorkflowSchemes.Should().NotContain(scheme);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }
}
