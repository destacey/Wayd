using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="CreateProjectTaskCommandHandler"/> against a real SQL Server container. The
/// handler numbers tasks with raw SQL and loads only the named deliverables, neither of which the in-memory fake
/// can run.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class CreateProjectTaskCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public CreateProjectTaskCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<Guid> StageId(Guid projectId, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        return await context.ProjectStages.Where(s => s.ProjectId == projectId).Select(s => s.Id).SingleAsync(ct);
    }

    private static CreateProjectTaskCommand Command(Guid projectId, Guid stageId, List<Guid> deliverableIds) =>
        new(projectId, "Release notes", null, ProjectTaskType.Task, Domain.Enums.TaskStatus.NotStarted,
            TaskPriority.Medium, new Progress(0m), stageId, null, null, null, null, deliverableIds);

    [Fact]
    public async Task Handle_WithDeliverableIds_CreatesTheTaskLinkedToThem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
        var stageId = await StageId(plan.ProjectId, ct);

        // Act
        CSharpFunctionalExtensions.Result<Application.ProjectTasks.Models.ProjectTaskIdAndKey> result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new CreateProjectTaskCommandHandler(context, NullLogger<CreateProjectTaskCommandHandler>.Instance)
                .Handle(Command(plan.ProjectId, stageId, [plan.PortalId, plan.ReportsId]), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var verify = _fixture.CreateContext();
        var linked = await verify.ProjectDeliverableTasks
            .Where(l => l.ProjectTaskId == result.Value.Id)
            .Select(l => l.ProjectDeliverableId)
            .ToListAsync(ct);
        linked.Should().BeEquivalentTo([plan.PortalId, plan.ReportsId]);
    }

    [Fact]
    public async Task Handle_WithADeliverableTheProjectDoesNotHave_SavesNothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
        var stageId = await StageId(plan.ProjectId, ct);
        var unknownDeliverableId = Guid.NewGuid();

        // Act
        CSharpFunctionalExtensions.Result<Application.ProjectTasks.Models.ProjectTaskIdAndKey> result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new CreateProjectTaskCommandHandler(context, NullLogger<CreateProjectTaskCommandHandler>.Instance)
                .Handle(Command(plan.ProjectId, stageId, [unknownDeliverableId]), ct);
        }

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(unknownDeliverableId.ToString());
        await using var verify = _fixture.CreateContext();
        (await verify.ProjectTasks.CountAsync(t => t.ProjectId == plan.ProjectId, ct)).Should().Be(3);
    }
}
