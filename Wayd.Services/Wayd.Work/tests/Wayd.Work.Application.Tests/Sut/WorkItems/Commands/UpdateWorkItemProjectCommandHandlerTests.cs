using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Commands;

public sealed class UpdateWorkItemProjectCommandHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly UpdateWorkItemProjectCommandHandler _handler;

    public UpdateWorkItemProjectCommandHandlerTests()
    {
        _handler = new UpdateWorkItemProjectCommandHandler(_dbContext, NullLogger<UpdateWorkItemProjectCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_Fails_WhenTheWorkItemIsInAnotherWorkspace()
    {
        // Arrange
        var workItem = new WorkItemFaker()
            .WithWorkspaceId(Guid.NewGuid())
            .WithType(new WorkTypeFaker().AsEpic().Generate())
            .Generate();
        _dbContext.AddWorkItem(workItem);

        // Act
        var result = await _handler.Handle(
            new UpdateWorkItemProjectCommand(Guid.NewGuid(), workItem.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Work item not found.");
        workItem.ProjectId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
