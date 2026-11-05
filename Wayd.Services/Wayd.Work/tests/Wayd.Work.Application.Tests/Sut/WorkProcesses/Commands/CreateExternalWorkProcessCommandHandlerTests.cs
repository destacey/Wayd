using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProcesses.Commands;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProcesses.Commands;

public sealed class CreateExternalWorkProcessCommandHandlerTests : IDisposable
{
    private const string WorkflowDescription = "Auto-generated workflow.";

    private readonly FakeWorkDbContext _dbContext = new();
    private readonly CreateExternalWorkProcessCommandHandler _handler;

    public CreateExternalWorkProcessCommandHandlerTests()
    {
        var dateTimeProvider = new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 10, 31, 12, 0)));
        _handler = new CreateExternalWorkProcessCommandHandler(_dbContext, dateTimeProvider, NullLogger<CreateExternalWorkProcessCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_CreatesTheProcessWithAWorkflowPerWorkTypeInOneSave()
    {
        // Arrange
        _dbContext.AddWorkTypes([new WorkTypeFaker().WithName("Story").Generate(), new WorkTypeFaker().WithName("Bug").Generate()]);
        _dbContext.AddWorkStatuses([new WorkStatusFaker().WithName("New").Generate(), new WorkStatusFaker().WithName("Done").Generate()]);
        var process = ExternalProcess("Agile", WorkType("Story", "New", "Done"), WorkType("Bug", "New", "Done"));

        // Act
        var result = await _handler.Handle(new CreateExternalWorkProcessCommand(process, WorkflowDescription), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _dbContext.Workflows.Select(w => w.Name).Should().BeEquivalentTo(["Agile - Story", "Agile - Bug"]);
        var workProcess = _dbContext.WorkProcesses.Single();
        workProcess.ExternalId.Should().Be(process.Id);
        // A workspace can only be created on an active work process.
        workProcess.IsActive.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        workProcess.Schemes.Select(s => s.WorkflowId).Should().BeEquivalentTo(_dbContext.Workflows.Select(w => w.Id));
    }

    [Fact]
    public async Task Handle_WhenAWorkStatusIsMissing_SavesNothing()
    {
        // Arrange — the first work type's workflow builds, the second's does not
        _dbContext.AddWorkTypes([new WorkTypeFaker().WithName("Story").Generate(), new WorkTypeFaker().WithName("Bug").Generate()]);
        _dbContext.AddWorkStatus(new WorkStatusFaker().WithName("New").Generate());
        var process = ExternalProcess("Agile", WorkType("Story", "New"), WorkType("Bug", "Triaged"));

        // Act
        var result = await _handler.Handle(new CreateExternalWorkProcessCommand(process, WorkflowDescription), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Triaged");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _dbContext.Workflows.Should().BeEmpty();
        _dbContext.WorkProcesses.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheExternalProcessAlreadyExists_Fails()
    {
        // Arrange
        var process = ExternalProcess("Agile");
        _dbContext.AddWorkProcess(new WorkProcessFaker().AsExternal(process.Id).Generate());

        // Act
        var result = await _handler.Handle(new CreateExternalWorkProcessCommand(process, WorkflowDescription), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private static IExternalWorkProcessConfiguration ExternalProcess(string name, params IExternalWorkTypeWorkflow[] workTypes)
    {
        var process = new Mock<IExternalWorkProcessConfiguration>();
        process.Setup(p => p.Id).Returns(Guid.CreateVersion7());
        process.Setup(p => p.Name).Returns(name);
        process.Setup(p => p.WorkTypes).Returns(workTypes.ToList());
        return process.Object;
    }

    private static IExternalWorkTypeWorkflow WorkType(string name, params string[] statusNames)
    {
        var states = statusNames
            .Select((statusName, i) => Mock.Of<IExternalWorkflowState>(s =>
                s.StatusName == statusName && s.Category == StatusCategory.Proposed && s.Order == i + 1 && s.IsActive))
            .ToList();

        var workType = new Mock<IExternalWorkTypeWorkflow>();
        workType.Setup(w => w.Name).Returns(name);
        workType.Setup(w => w.IsActive).Returns(true);
        workType.Setup(w => w.WorkflowStates).Returns(states);
        return workType.Object;
    }
}
