using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;
using static Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure.ProposedProjectWithStage;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="DeleteProjectTaskCommandHandler"/> against a real SQL Server container.
/// <para>
/// A removed dependency is a soft delete whose row still references both tasks, and the database cascades
/// from neither end, so the handler must load the task's dependencies for the delete to take them with it.
/// The in-memory fake has no foreign keys and cannot tell.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class DeleteProjectTaskCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ForATaskWithARemovedDependency_DeletesTheTaskAndTheRemovedDependency(bool deletePredecessor)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (predecessorId, successorId) = await SeedDependency(remove: true, ct);
        var deletedId = deletePredecessor ? predecessorId : successorId;
        var keptId = deletePredecessor ? successorId : predecessorId;

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Handler(context).Handle(new DeleteProjectTaskCommand(deletedId), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.ProjectTasks.AnyAsync(t => t.Id == deletedId, ct)).Should().BeFalse();
        (await verify.ProjectTasks.AnyAsync(t => t.Id == keptId, ct)).Should().BeTrue();
        (await verify.ProjectTaskDependencies.AnyAsync(d => d.PredecessorId == predecessorId || d.SuccessorId == successorId, ct))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForATaskWithAnActiveDependency_RefusesAndKeepsBothTasks()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (predecessorId, successorId) = await SeedDependency(remove: false, ct);

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Handler(context).Handle(new DeleteProjectTaskCommand(predecessorId), ct);
        }

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Cannot delete a task with active dependencies. Remove dependencies first.");

        await using var verify = _fixture.CreateContext();
        (await verify.ProjectTasks.CountAsync(t => t.Id == predecessorId || t.Id == successorId, ct)).Should().Be(2);
        (await verify.ProjectTaskDependencies.CountAsync(d => d.PredecessorId == predecessorId && d.SuccessorId == successorId, ct))
            .Should().Be(1);
    }

    private static DeleteProjectTaskCommandHandler Handler(WaydDbContext context) =>
        new(context, NullLogger<DeleteProjectTaskCommandHandler>.Instance);

    /// <summary>
    /// Two sibling tasks in a project, the first a predecessor of the second, each step saved separately as the
    /// add and remove endpoints save it.
    /// </summary>
    private async Task<(Guid PredecessorId, Guid SuccessorId)> SeedDependency(bool remove, CancellationToken ct)
    {
        var seed = await Seed(_fixture, ct);

        Guid predecessorId, successorId;
        await using (var context = _fixture.CreateContext())
        {
            var project = await context.Projects
                .Include(p => p.Stages)
                .Include(p => p.Tasks)
                .SingleAsync(p => p.Id == seed.ProjectId, ct);
            var predecessor = CreateTask(project, 1, "Design", seed.StageId);
            var successor = CreateTask(project, 2, "Build", seed.StageId);
            predecessor.AddDependency(successor).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(ct);
            (predecessorId, successorId) = (predecessor.Id, successor.Id);
        }

        if (remove)
        {
            await using var context = _fixture.CreateContext();
            var predecessor = await context.ProjectTasks
                .Include(t => t.Successors)
                .SingleAsync(t => t.Id == predecessorId, ct);
            predecessor.RemoveDependency(successorId, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(ct);
        }

        return (predecessorId, successorId);
    }
}
