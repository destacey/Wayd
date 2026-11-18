using CSharpFunctionalExtensions;
using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Imports;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Imports;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;
using Moq;
using Wayd.Common.Application.Interfaces;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Imports;

public sealed class PlanningIntervalObjectiveImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ILinkWriter> _linkWriter = new();
    private readonly PlanningIntervalObjectiveImportDefinition _definition;

    private readonly PlanningInterval _interval;
    private readonly PlanningTeam _team;

    public PlanningIntervalObjectiveImportDefinitionTests()
    {
        _definition = new PlanningIntervalObjectiveImportDefinition(
            _dbContext,
            Mock.Of<IDateTimeProvider>(),
            Mock.Of<ICurrentUser>(),
            _linkWriter.Object,
            new ImportPayloadSerializer());
        _linkWriter
            .Setup(w => w.Add(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _interval = new PlanningIntervalFaker().Generate();
        _team = new PlanningTeamFaker(TeamType.Team).Generate();

        _dbContext.AddPlanningInterval(_interval);
        _dbContext.AddPlanningTeam(_team);
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportPlanningIntervalObjectiveDto Row(Guid? intervalId = null, Guid? teamId = null, ObjectiveStatus status = ObjectiveStatus.NotStarted, double progress = 0, Instant? closedDate = null) =>
        new(
            intervalId ?? _interval.Id,
            teamId ?? _team.Id,
            "Ship the thing",
            "  With a description  ",
            status,
            progress,
            null,
            null,
            false,
            closedDate,
            3);

    private Task<Result<ImportPassResult>> Run(params ImportPlanningIntervalObjectiveDto[] objectives) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(),
            CreatePass,
            [.. objectives.Select((o, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(o)))],
            isFinalChunk: true,
            TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicBecauseTheObjectiveIsSavedWithTheRun()
    {
        // Arrange & Act & Assert — the objective is a row on the planning interval, so the runner's
        // single save either keeps every row or none
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.MaxRows.Should().Be(10_000);
    }

    [Fact]
    public async Task CreateObjectives_AddsTheObjectiveToThePlanningInterval()
    {
        // Arrange & Act
        var result = await Run(Row());

        // Assert
        var row = result.Value.Rows.Single();
        row.Failed.Should().BeFalse();
        var objective = _interval.Objectives.Should().ContainSingle().Subject;
        row.CreatedEntityId.Should().Be(objective.Id);
        objective.Name.Should().Be("Ship the thing");
        objective.Description.Should().Be("With a description");
        objective.TeamId.Should().Be(_team.Id);
        objective.Type.Should().Be(PlanningIntervalObjectiveType.Team);
        objective.Order.Should().Be(3);
    }

    [Fact]
    public async Task CreateObjectives_AddsTheRowsLinks_ToTheObjectiveItCreated()
    {
        // Arrange
        var row = Row() with { Links = "Epic|https://example.com/epic" };

        // Act
        var result = await Run(row);

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();
        var objectiveId = _interval.Objectives.Should().ContainSingle().Subject.Id;

        _linkWriter.Verify(w => w.Add("planning.planning-interval-objective", objectiveId, "Epic", "https://example.com/epic",
            It.Is<EventActor>(a => a.Kind == EventActorKind.Import), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateObjectives_RejectsARowWithAMalformedLink_CreatingNeitherTheObjectiveNorAnyLink()
    {
        // Arrange
        var row = Row() with { Links = "|https://example.com/epic" };

        // Act
        var result = await Run(row);

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().StartWith("Links entry '|https://example.com/epic'");
        _interval.Objectives.Should().BeEmpty();
        _linkWriter.Verify(w => w.Add(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EventActor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateObjectives_AddsEachObjectiveToThePlanningIntervalItsRowNames()
    {
        // Arrange
        var other = new PlanningIntervalFaker().Generate();
        _dbContext.AddPlanningInterval(other);

        // Act
        var result = await Run(Row(), Row(intervalId: other.Id));

        // Assert
        result.Value.Rows.Should().OnlyContain(r => !r.Failed);
        result.Value.Rows.Single(r => r.ImportId == "r1").CreatedEntityId
            .Should().Be(_interval.Objectives.Should().ContainSingle().Subject.Id);
        result.Value.Rows.Single(r => r.ImportId == "r2").CreatedEntityId
            .Should().Be(other.Objectives.Should().ContainSingle().Subject.Id);
    }

    [Fact]
    public async Task CreateObjectives_KeepsTheImportedStatusProgressAndClosedDate()
    {
        // Arrange
        var closed = Instant.FromUtc(2026, 3, 1, 12, 0);

        // Act
        var result = await Run(Row(status: ObjectiveStatus.Completed, progress: 100, closedDate: closed));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        var objective = _interval.Objectives.Single();
        objective.Status.Should().Be(ObjectiveStatus.Completed);
        objective.Progress.Should().Be(100);
        objective.ClosedDate.Should().Be(closed);
    }

    [Fact]
    public async Task CreateObjectives_RejectsARowNamingAnIntervalThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(Row(intervalId: Guid.CreateVersion7()));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
        _interval.Objectives.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateObjectives_RejectsARowNamingATeamThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(Row(teamId: Guid.CreateVersion7()));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
        _interval.Objectives.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateObjectives_RejectsEveryRowWhenObjectivesAreLocked()
    {
        // Arrange
        var locked = new PlanningIntervalFaker().WithObjectivesLocked(true).Generate();
        _dbContext.AddPlanningInterval(locked);

        // Act
        var result = await Run(Row(intervalId: locked.Id));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
        result.Value.Rows.Single().Error.Should().Contain("locked");
        locked.Objectives.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateObjectives_KeepsTheGoodRowsWhenOneIsRejected()
    {
        // Arrange — the runner decides what an atomic run does with a rejected row; the pass itself
        // still reports each row on its own
        var good = Row();
        var bad = Row(teamId: Guid.CreateVersion7());

        // Act
        var result = await Run(good, bad);

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        _interval.Objectives.Should().ContainSingle();
    }
}
