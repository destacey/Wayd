using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Links;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;
using static Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure.ProposedProjectWithStage;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="DeleteProjectCommandHandler"/> against a real SQL Server container.
/// <para>
/// The database cascades a project's tasks but not the dependencies between them, so a project whose tasks
/// have a dependency, active or removed, deletes only if the handler loads them. The in-memory fake has no
/// foreign keys and cannot tell.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class DeleteProjectCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ForAProposedProjectWithTaskDependencies_DeletesTheProjectItsTasksAndTheirDependencies()
    {
        // Arrange — a nested task, an active dependency and a removed one.
        var ct = TestContext.Current.CancellationToken;
        var seed = await Seed(_fixture, ct);

        await using (var context = _fixture.CreateContext())
        {
            var project = await context.Projects
                .Include(p => p.Stages)
                .Include(p => p.Tasks)
                .SingleAsync(p => p.Id == seed.ProjectId, ct);
            var design = CreateTask(project, 1, "Design", seed.StageId);
            var build = CreateTask(project, 2, "Build", seed.StageId);
            var buildApi = CreateTask(project, 3, "Build API", build.Id);
            design.AddDependency(buildApi).IsSuccess.Should().BeTrue();
            design.AddDependency(build).IsSuccess.Should().BeTrue();
            design.RemoveDependency(build.Id, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(ct);
        }

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await Handler(context).Handle(new DeleteProjectCommand(seed.ProjectId), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.Projects.AnyAsync(p => p.Id == seed.ProjectId, ct)).Should().BeFalse();
        (await verify.ProjectTasks.AnyAsync(t => t.ProjectId == seed.ProjectId, ct)).Should().BeFalse();
        (await verify.ProjectTaskDependencies.AnyAsync(ct)).Should().BeFalse("the seeded project held every dependency");
    }

    private static DeleteProjectCommandHandler Handler(WaydDbContext context)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");

        return new DeleteProjectCommandHandler(
            context,
            currentUser.Object,
            NullLogger<DeleteProjectCommandHandler>.Instance,
            ActiveProjectWithDeliverable.Clock(),
            Mock.Of<IDiscussionWriter>(),
            Mock.Of<ILinkWriter>(),
            Mock.Of<IMarkdownAttachments>(),
            Mock.Of<IKpiWriter>());
    }
}
