using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
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

    private static CreateProjectTaskCommand Command(Guid projectId, Guid stageId, List<Guid> deliverableIds,
        int? order = null) =>
        new(projectId, "Release notes", null, ProjectTaskType.Task, Domain.Enums.TaskStatus.NotStarted,
            TaskPriority.Medium, new Progress(0m), stageId, null, null, null, null, deliverableIds, order);

    [Fact]
    public async Task Handle_WithAnOrder_CreatesTheTaskAtThatPlace()
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
                .Handle(Command(plan.ProjectId, stageId, [], order: 2), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var verify = _fixture.CreateContext();
        var orders = await verify.ProjectTasks
            .Where(t => t.ProjectId == plan.ProjectId && t.ParentId == null)
            .OrderBy(t => t.Order)
            .Select(t => new { t.Id, t.Order })
            .ToListAsync(ct);
        orders.Select(t => t.Order).Should().Equal(1, 2, 3, 4);
        orders[1].Id.Should().Be(result.Value.Id);
    }

    [Fact]
    public async Task Handle_WithoutAnOrder_CreatesTheTaskLast()
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
                .Handle(Command(plan.ProjectId, stageId, []), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var verify = _fixture.CreateContext();
        var last = await verify.ProjectTasks
            .Where(t => t.ProjectId == plan.ProjectId && t.ParentId == null)
            .OrderByDescending(t => t.Order)
            .Select(t => t.Id)
            .FirstAsync(ct);
        last.Should().Be(result.Value.Id);
    }

    /// <summary>
    /// A Proposed project with one stage and, when <paramref name="withTasks"/>, the linked plan's three tasks;
    /// without them there are no task rows for a create to lock on.
    /// </summary>
    private async Task<(Guid ProjectId, Guid StageId)> SeedProject(bool withTasks, CancellationToken ct)
    {
        if (withTasks)
        {
            var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
            return (plan.ProjectId, await StageId(plan.ProjectId, ct));
        }

        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, ct, activateTheProject: false);
        await using var context = _fixture.CreateContext();
        var lifecycle = ProjectLifecycle.Create("Standard", "Standard delivery lifecycle", [("Delivery", "Delivery stage")]);
        lifecycle.Activate();
        await context.ProjectLifecycles.AddAsync(lifecycle, ct);
        var project = await context.Projects.SingleAsync(p => p.Id == seed.ProjectId, ct);
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, SqlServerDbContextFixture.FixedNow)
            .IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);
        return (project.Id, project.Stages.Single().Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_CreatedConcurrently_GivesEachTaskItsOwnNumber(bool withTasks)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (projectId, stageId) = await SeedProject(withTasks, ct);
        int existing;
        await using (var context = _fixture.CreateContext())
            existing = await context.ProjectTasks.CountAsync(t => t.ProjectId == projectId, ct);
        const int creates = 8;

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, creates).Select(async _ =>
        {
            await using var context = _fixture.CreateContext();
            return await new CreateProjectTaskCommandHandler(context, NullLogger<CreateProjectTaskCommandHandler>.Instance)
                .Handle(Command(projectId, stageId, []), ct);
        }));

        // Assert
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error : null));
        await using var verify = _fixture.CreateContext();
        var numbers = await verify.ProjectTasks
            .Where(t => t.ProjectId == projectId)
            .Select(t => t.Number)
            .ToListAsync(ct);
        numbers.Should().BeEquivalentTo(Enumerable.Range(1, existing + creates));
    }

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
