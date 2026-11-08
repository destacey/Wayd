using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared.Extensions;
using Wayd.Tests.Shared.Infrastructure;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProcesses;
using Wayd.Work.Application.WorkProcesses.Queries;
using Wayd.Work.Domain;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProcesses.Queries;

public sealed class GetWorkProcessSchemesQueryHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 8, 9, 0);
    private static readonly string OwnerType = WorkWorkflowOwners.WorkItem.Key;

    private readonly FakeWorkDbContext _dbContext = new();
    private readonly List<StatusWorkflow> _engineWorkflows = [];
    private readonly List<StatusWorkflowScheme> _engineSchemes = [];
    private readonly GetWorkProcessSchemesQueryHandler _handler;

    public GetWorkProcessSchemesQueryHandlerTests()
    {
        MapsterTestConfiguration.Ensure();
        WorkWorkflowOwners.Register();

        var statusWorkflowDbContext = new Mock<IStatusWorkflowDbContext>();
        statusWorkflowDbContext.Setup(c => c.StatusWorkflows).Returns(() => _engineWorkflows.AsDbSet());
        statusWorkflowDbContext.Setup(c => c.StatusWorkflowSchemes).Returns(() => _engineSchemes.AsDbSet());

        _handler = new GetWorkProcessSchemesQueryHandler(_dbContext, statusWorkflowDbContext.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    private static StatusWorkflow Workflow(string name, params SourceStatus[] statuses) =>
        StatusWorkflow.CreateManaged(name, OwnerType, Guid.NewGuid().ToString(), statuses, EventActor.System, Now).Value;

    private static RecordType RecordType(string name) =>
        Common.Domain.StatusWorkflows.RecordType.CreateManaged(OwnerType, name, name, EventActor.System, Now).Value;

    private void AddEngineScheme(Guid processExternalId, params (RecordType RecordType, StatusWorkflow Workflow)[] entries)
    {
        _engineWorkflows.AddRange(entries.Select(e => e.Workflow).Where(w => !_engineWorkflows.Contains(w)));
        _engineSchemes.Add(StatusWorkflowScheme.CreateManaged(
            OwnerType, "Agile", ManagedWorkflowKeys.Scheme(processExternalId), entries[0].Workflow, EventActor.System, Now, entries).Value);
    }

    private WorkType AddWorkType(string name, RecordType? recordType)
    {
        var workType = new WorkTypeFaker().WithName(name).Generate();
        if (recordType is not null)
            workType.LinkRecordType(recordType.Id);
        _dbContext.AddWorkType(workType);
        return workType;
    }

    private WorkProcess AddProcess(Guid? externalId, params WorkType[] workTypes)
    {
        var schemes = workTypes.Select(t => new WorkProcessSchemeFaker().WithWorkType(t).Generate()).ToList();
        var faker = externalId is Guid id ? new WorkProcessFaker().AsExternal(id) : new WorkProcessFaker().WithExternalId(null);
        var process = faker.WithSchemes(schemes).Generate();
        _dbContext.AddWorkProcess(process);
        return process;
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoWorkProcessHasTheId()
    {
        // Arrange
        _dbContext.AddWorkProcess(new WorkProcessFaker().Generate());

        // Act
        var result = await _handler.Handle(new GetWorkProcessSchemesQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsAnEmptyList_WhenTheWorkProcessHasNoSchemes()
    {
        // Arrange
        var workProcess = new WorkProcessFaker().Generate();
        _dbContext.AddWorkProcess(workProcess);

        // Act
        var result = await _handler.Handle(new GetWorkProcessSchemesQuery(workProcess.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsTheEngineWorkflowOfEachWorkType_WithItsStatuses()
    {
        // Arrange
        var externalId = Guid.NewGuid();
        var bugType = RecordType("Bug");
        var storyType = RecordType("User Story");
        var bugWorkflow = Workflow("Agile - Bug",
            new SourceStatus("new", "New", StatusCategory.Proposed),
            new SourceStatus("resolved", "Resolved", StatusCategory.Done, IsHidden: true),
            new SourceStatus("closed", "Closed", StatusCategory.Done));
        var storyWorkflow = Workflow("Agile - User Story", new SourceStatus("active", "Active", StatusCategory.Active));
        bugWorkflow.SetPrivate(w => w.Key, 11);
        AddEngineScheme(externalId, (bugType, bugWorkflow), (storyType, storyWorkflow));
        var bug = AddWorkType("Bug", bugType);
        var story = AddWorkType("User Story", storyType);
        var process = AddProcess(externalId, story, bug);

        // Act
        var result = await _handler.Handle(new GetWorkProcessSchemesQuery(process.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        var schemes = result!;
        schemes.Select(s => (s.WorkType.Name, s.Workflow?.Name)).Should().Equal(("Bug", "Agile - Bug"), ("User Story", "Agile - User Story"));

        var workflow = schemes[0].Workflow!;
        (workflow.Id, workflow.Key).Should().Be((bugWorkflow.Id, 11));
        workflow.Statuses.Select(s => (s.Id, s.Name, s.Category.Id, s.Category.Name, s.IsRetired)).Should().Equal(
            (bugWorkflow.Statuses.Single(s => s.Name == "New").Id, "New", (int)StatusCategory.Proposed, "Proposed", false),
            (bugWorkflow.Statuses.Single(s => s.Name == "Closed").Id, "Closed", (int)StatusCategory.Done, "Done", false),
            (bugWorkflow.Statuses.Single(s => s.Name == "Resolved").Id, "Resolved", (int)StatusCategory.Done, "Done", true));
        workflow.Statuses.Select(s => s.Order).Should().BeInAscendingOrder();
        schemes[0].WorkType.Level.Name.Should().Be(bug.Level!.Name);
    }

    [Fact]
    public async Task Handle_GivesNoWorkflow_ToATypeWithoutAnEntryOfItsOwn()
    {
        // Arrange — the scheme's default belongs to the Bug type
        var externalId = Guid.NewGuid();
        var bugType = RecordType("Bug");
        AddEngineScheme(externalId, (bugType, Workflow("Agile - Bug", new SourceStatus("new", "New", StatusCategory.Proposed))));
        var bug = AddWorkType("Bug", bugType);
        var task = AddWorkType("Task", RecordType("Task"));
        var unlinked = AddWorkType("Epic", recordType: null);
        var process = AddProcess(externalId, bug, task, unlinked);

        // Act
        var result = await _handler.Handle(new GetWorkProcessSchemesQuery(process.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.Select(s => (s.WorkType.Name, s.Workflow?.Name)).Should().Equal(("Bug", "Agile - Bug"), ("Epic", null), ("Task", null));
    }

    [Fact]
    public async Task Handle_ReadsOnlyItsOwnProcesssEngineScheme()
    {
        // Arrange
        var bugType = RecordType("Bug");
        AddEngineScheme(Guid.NewGuid(), (bugType, Workflow("Scrum - Bug", new SourceStatus("new", "New", StatusCategory.Proposed))));
        var process = AddProcess(Guid.NewGuid(), AddWorkType("Bug", bugType));

        // Act
        var result = await _handler.Handle(new GetWorkProcessSchemesQuery(process.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.Should().ContainSingle().Which.Workflow.Should().BeNull();
    }

    [Fact]
    public async Task Handle_GivesNoWorkflows_ToAProcessWithNoExternalId()
    {
        // Arrange
        var bugType = RecordType("Bug");
        var process = AddProcess(externalId: null, AddWorkType("Bug", bugType));

        // Act
        var result = await _handler.Handle(new GetWorkProcessSchemesQuery(process.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.Should().ContainSingle().Which.Workflow.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReportsWhetherEachWorkTypeIsActiveInTheProcess()
    {
        // Arrange
        var bug = AddWorkType("Bug", recordType: null);
        var process = new WorkProcessFaker().AsExternal(Guid.NewGuid())
            .WithSchemes([new WorkProcessSchemeFaker().WithWorkType(bug).WithIsActive(false).Generate()])
            .Generate();
        _dbContext.AddWorkProcess(process);

        // Act
        var result = await _handler.Handle(new GetWorkProcessSchemesQuery(process.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.Should().ContainSingle().Which.IsActive.Should().BeFalse();
    }
}
